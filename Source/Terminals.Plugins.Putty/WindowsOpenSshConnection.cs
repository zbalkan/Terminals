using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Terminals.Common.Configuration;
using Terminals.Common.Forms;
using Terminals.Connections;

namespace Terminals.Plugins.Putty
{
    /// <summary>Launches ssh.exe in an independent console; ConPTY is not implemented.</summary>
    internal sealed class WindowsOpenSshConnection : Connection
    {
        private Process process;
        private int closing;
        private int starting;

        public override bool ConnectionPending
        {
            get { return Volatile.Read(ref starting) != 0; }
        }

        public override bool Connected
        {
            get
            {
                var active = Interlocked.CompareExchange(ref process, null, null);
                if (active == null) return false;
                try { return !active.HasExited; }
                catch (InvalidOperationException) { return false; }
            }
        }

        public WindowsOpenSshConnection()
        {
            Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Windows OpenSSH runs in a separate console window.\r\n" +
                       "Authentication and host-key verification are managed by OpenSSH."
            });
        }

        public override bool Connect()
        {
            // Construct the process arguments on the UI thread; only process
            // creation and lifetime handling run on a background thread.
            string executable = SshClientPreference.FindWindowsOpenSsh();
            if (executable == null)
            {
                LastError = "Windows OpenSSH Client is not installed. Install it under Windows Optional Features or select PuTTY.";
                return false;
            }

            ProcessStartInfo start;
            try
            {
                var credentials = ResolveFavoriteCredentials();
                start = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = BuildArguments(Favorite.ServerName, Favorite.Port, credentials.UserName),
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                LastError = "Unable to prepare Windows OpenSSH: " + ex.Message;
                return false;
            }

            // Process.Start can block on Windows shell interaction and must not
            // run inside the synchronous WinForms connection factory.
            Volatile.Write(ref starting, 1);
            ExternalProcessLauncher.RunAsync(() => StartInBackground(start));
            return true;
        }

        private void StartInBackground(ProcessStartInfo start)
        {
            if (Volatile.Read(ref closing) != 0) return;
            Process launched = null;
            try
            {
                launched = new Process { StartInfo = start, EnableRaisingEvents = true };
                launched.Exited += OnProcessExited;
                if (Volatile.Read(ref closing) != 0) return;
                if (!launched.Start())
                    throw new InvalidOperationException("The Windows OpenSSH process did not start.");

                // Publish the process before notifying the UI; closing the tab
                // while startup is pending must terminate the newly started process.
                Interlocked.Exchange(ref process, launched);
                Volatile.Write(ref starting, 0);
                if (Volatile.Read(ref closing) != 0)
                {
                    var abandoned = Interlocked.Exchange(ref process, null);
                    if (abandoned != null) TerminateProcess(abandoned);
                }
                launched = null;
            }
            catch (Exception ex) when (ex is Win32Exception || ex is IOException ||
                                       ex is InvalidOperationException || ex is ArgumentException)
            {
                PostToUi(() =>
                {
                    if (Volatile.Read(ref closing) != 0) return;
                    Volatile.Write(ref starting, 0);
                    LastError = "Unable to launch Windows OpenSSH: " + ex.Message;
                    MessageBox.Show(LastError, "SSH", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    FireDisconnected();
                });
            }
            finally
            {
                // A launched process is owned by the connection. Only dispose
                // the temporary instance when it was never handed over.
                if (launched != null)
                {
                    launched.Exited -= OnProcessExited;
                    TerminateProcess(launched);
                }
            }
        }

        private void OnProcessExited(object sender, EventArgs args)
        {
            // Exited runs on a ThreadPool thread. Never call the tab remover
            // from here: it must execute on the WinForms message loop.
            PostToUi(() =>
            {
                if (Volatile.Read(ref closing) == 0)
                    FireDisconnected();
            });
        }

        private void PostToUi(Action action)
        {
            try
            {
                var target = ParentForm as Control;
                if (target != null && target.IsHandleCreated && !target.IsDisposed)
                    target.BeginInvoke(new MethodInvoker(action));
            }
            catch (InvalidOperationException) { } // Form closing
        }

        private static void TerminateProcess(Process launched)
        {
            try { if (!launched.HasExited) launched.Kill(); }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
            finally { launched.Dispose(); }
        }

        internal static string BuildArguments(string host, int port, string username)
        {
            if (string.IsNullOrWhiteSpace(host) || host.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("An SSH host is required.", "host");
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException("port");
            string args = "-p " + port;
            if (!string.IsNullOrWhiteSpace(username))
                args += " -l " + Quote(username);
            return args + " -- " + Quote(host);
        }

        private static string Quote(string value)
        {
            var result = new System.Text.StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"')
                {
                    result.Append('\\', slashes * 2 + 1);
                    result.Append('"');
                    slashes = 0;
                    continue;
                }
                result.Append('\\', slashes);
                slashes = 0;
                result.Append(c);
            }
            result.Append('\\', slashes * 2);
            result.Append('"');
            return result.ToString();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref closing, 1) == 0)
            {
                Volatile.Write(ref starting, 0);
                var active = Interlocked.Exchange(ref process, null);
                if (active != null)
                {
                    active.Exited -= OnProcessExited;
                    // No Kill/WaitForExit on the UI thread.
                    ExternalProcessLauncher.StopDetached(active);
                }
            }
            base.Dispose(disposing);
        }
    }
}
