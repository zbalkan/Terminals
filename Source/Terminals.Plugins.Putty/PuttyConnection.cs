using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using Terminals.Common.Forms;
using System.Windows.Forms;
using Terminals.Connections;
using Terminals.Data;
using Terminals.Native;

namespace Terminals.Plugins.Putty
{
    internal class PuttyConnection : Connection, IFocusable
    {
        private bool windowCaptured;
        private int closing;
        private int starting;
        
        private Process puttyProcess;

        public override bool ConnectionPending
        {
            get { return Volatile.Read(ref starting) != 0; }
        }

        public override bool Connected
        {
            get
            {
                return this.windowCaptured && this.ProcessRunning;
            }
        }

        private bool ProcessRunning
        {
            get
            {
                return this.puttyProcess != null && !this.puttyProcess.HasExited;
            }
        }

        public PuttyConnection()
        {
            this.Resize += this.PuttyConnection_Resize;
            this.GotFocus += this.PuttyConnection_GotFocus;
        }

        private void PuttyConnection_GotFocus(object sender, EventArgs e)
        {
            this.SendFocusToPutty();
        }

        private void PuttyConnection_Resize(object sender, EventArgs e)
        {
            if (this.windowCaptured && !this.IsDisposed)
                this.ClipPutty();
        }

        private void ClipPutty()
        {
            if (!this.Connected)
                return;

            Rectangle windowRect = Methods.GetWindowRect(this.puttyProcess.MainWindowHandle);

            Rectangle clientRect = Methods.GetClientRect(this.puttyProcess.MainWindowHandle);

            Point referencePoint0 = new Point(0, 0);
            Methods.ClientToScreen(this.puttyProcess.MainWindowHandle, ref referencePoint0);

            Point referencePoint1 = new Point(clientRect.Width, clientRect.Height);
            Methods.ClientToScreen(this.puttyProcess.MainWindowHandle, ref referencePoint1);

            int top = referencePoint0.Y - windowRect.Top;
            int left = referencePoint0.X - windowRect.Left;
            int bottom = windowRect.Bottom - referencePoint1.Y;

            int width = this.Width + left + left; // + right ( using doubled left )
            int height = this.Height + top + bottom;

            Methods.SetWindowPos(this.puttyProcess.MainWindowHandle, IntPtr.Zero, -left, -top, width, height, SetWindowPosFlags.FrameChanged | SetWindowPosFlags.DoNotActivate);
        }

        private void AdjustWindowStyle(IntPtr handle)
        {
            uint lStyle = Methods.GetWindowLong(handle, (int) WindowLongParam.GWL_STYLE);
            WindowStyles flagsToDisable = ~(WindowStyles.WS_CAPTION | WindowStyles.WS_THICKFRAME | WindowStyles.WS_MINIMIZE | WindowStyles.WS_MAXIMIZE | WindowStyles.WS_SYSMENU);
            lStyle &= (uint)flagsToDisable;
            Methods.SetWindowLong(handle, (int)WindowLongParam.GWL_STYLE, lStyle);

            uint lExStyle = Methods.GetWindowLong(handle, (int)WindowLongParam.GWL_EXSTYLE);
            WindowExStyles flagsToDisableEx = ~(WindowExStyles.WS_EX_DLGMODALFRAME | WindowExStyles.WS_EX_CLIENTEDGE | WindowExStyles.WS_EX_STATICEDGE);
            lExStyle &= (uint)flagsToDisableEx;
            Methods.SetWindowLong(handle, (int)WindowLongParam.GWL_EXSTYLE, lExStyle);
        }

        public override bool Connect()
        {
            this.Dock = DockStyle.Fill;
            try
            {
                // Read favorites and credentials while still on the UI thread.
                // Process creation and WaitForInputIdle must never block it.
                string executable = Executables.GetPuttyBinaryPath();
                IGuardedSecurity credentials = this.ResolveFavoriteCredentials();
                string arguments = new ArgumentsBuilder(credentials, this.Favorite).Build();
                var launched = new Process();
                launched.StartInfo.FileName = executable;
                launched.StartInfo.Arguments = arguments;
                launched.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                launched.EnableRaisingEvents = true;
                launched.Exited += this.PuttyProcessOnExited;
                this.puttyProcess = launched;
                Volatile.Write(ref starting, 1);
                ExternalProcessLauncher.RunAsync(() => this.LaunchPuttyProcess(launched));
                return true; // UI remains responsive during process startup.
            }
            catch (System.IO.FileNotFoundException ex)
            {
                Logging.Error("PuTTY executable is unavailable.", ex);
                LastError = ex.Message;
                return false;
            }
        }

        private void LaunchPuttyProcess(Process launched)
        {
            try
            {
                if (Volatile.Read(ref closing) != 0)
                    return;
                if (!launched.Start())
                    throw new InvalidOperationException("PuTTY did not start.");

                // Bound the wait and keep it on the worker. A slow or broken PuTTY
                // cannot freeze the WinForms message loop.
                launched.WaitForInputIdle(5000);
                if (Volatile.Read(ref closing) != 0)
                    return;

                var form = ParentForm as Control;
                if (form != null && form.IsHandleCreated && !form.IsDisposed)
                    form.BeginInvoke(new MethodInvoker(() => CapturePuttyWindow(launched)));
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception ||
                                       ex is InvalidOperationException || ex is System.IO.IOException ||
                                       ex is ObjectDisposedException)
            {
                Logging.Error("Unable to start or embed PuTTY.", ex);
                var form = ParentForm as Control;
                if (form != null && form.IsHandleCreated && !form.IsDisposed)
                {
                    try
                    {
                        form.BeginInvoke(new MethodInvoker(() =>
                        {
                            if (Volatile.Read(ref closing) != 0) return;
                            LastError = "Unable to start PuTTY: " + ex.Message;
                            MessageBox.Show(LastError, "PuTTY", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            FireDisconnected();
                        }));
                    }
                    catch (InvalidOperationException) { }
                }
            }
        }

        private void CapturePuttyWindow(Process launched)
        {
            if (Volatile.Read(ref closing) != 0 || this.IsDisposed) return;
            try
            {
                if (launched.HasExited)
                {
                    FireDisconnected();
                    return;
                }

                IntPtr handle = launched.MainWindowHandle;
                if (handle == IntPtr.Zero)
                    throw new InvalidOperationException("PuTTY did not create a terminal window.");

                AdjustWindowStyle(handle);
                Methods.SetParent(handle, this.Handle);
                this.windowCaptured = true;
                Volatile.Write(ref starting, 0);
                ClipPutty();
            }
            catch (Exception ex) when (ex is InvalidOperationException ||
                                       ex is System.ComponentModel.Win32Exception)
            {
                Logging.Error("Unable to embed PuTTY window.", ex);
                FireDisconnected();
            }
        }

        private void PuttyProcessOnExited(object sender, EventArgs eventArgs)
        {
            Volatile.Write(ref starting, 0);
            if (Volatile.Read(ref closing) == 0)
                FireDisconnected(); // TabControlRemover marshals to WinForms.
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref closing, 1) == 0)
            {
                Volatile.Write(ref starting, 0);
                var launched = Interlocked.Exchange(ref puttyProcess, null);
                if (launched != null)
                {
                    launched.Exited -= this.PuttyProcessOnExited;
                    ExternalProcessLauncher.StopDetached(launched);
                }
            }
            base.Dispose(disposing);
        }

        void IFocusable.Focus()
        {
            this.SendFocusToPutty();
        }

        private void SendFocusToPutty()
        {
            if (!this.Connected)
                return;

            if (this.IsDisposed) return;
            this.BeginInvoke(new MethodInvoker(() =>
            {
                IntPtr puttyWindow = this.puttyProcess.MainWindowHandle;
                Methods.SetForegroundWindow(puttyWindow);
                Methods.SetFocus(puttyWindow);
            }));
        }
    }
}
