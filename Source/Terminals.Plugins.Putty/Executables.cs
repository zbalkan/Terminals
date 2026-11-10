using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Terminals.Common.Configuration;
using Terminals.Common.Forms;
using Terminals.Native;

namespace Terminals.Plugins.Putty
{
    internal class Executables
    {
        internal const string PUTTY_BINARY = "putty.exe";
        private const string PAGEANT = "pageant";
        internal const string PAGEANT_BINARY = PAGEANT + ".exe";

        internal static void LaunchPutty()
        {
            var ui = SynchronizationContext.Current;
            ExternalProcessLauncher.RunAsync(() =>
            {
                try { using (var p = Process.Start(new ProcessStartInfo(GetPuttyBinaryPath()) { UseShellExecute = true })) { } }
                catch (Exception ex) when (ex is FileNotFoundException || ex is Win32Exception ||
                                           ex is InvalidOperationException)
                {
                    ExternalProcessLauncher.Post(ui, () => ShowError(ex.Message, PUTTY_BINARY));
                }
            });
        }

        internal static void LaunchPageant()
        {
            var ui = SynchronizationContext.Current;
            // Process discovery, process launch and WaitForInputIdle may block.
            // Never execute them synchronously from a WinForms click handler.
            ExternalProcessLauncher.RunAsync(() =>
            {
                try
                {
                    IntPtr handle = FindOrLaunchPageantWindow();
                    ExternalProcessLauncher.Post(ui, () =>
                    {
                        if (handle == IntPtr.Zero)
                        {
                            MessageBox.Show(Properties.Resources.PageantMessage, PAGEANT_BINARY,
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                        Methods.ShowWindowAsync(handle, (int)ShowWindowCommands.ShowDefault);
                        Methods.ShowWindowAsync(handle, (int)ShowWindowCommands.Show);
                        Methods.SetForegroundWindow(handle);
                    });
                }
                catch (Exception ex) when (ex is FileNotFoundException || ex is Win32Exception ||
                                           ex is InvalidOperationException || ex is IOException)
                {
                    ExternalProcessLauncher.Post(ui, () => ShowError(ex.Message, PAGEANT_BINARY));
                }
            });
        }

        private static IntPtr FindOrLaunchPageantWindow()
        {
            // A newly started Pageant may need a short moment to register a
            // process and create its main window. Poll on the worker only.
            bool launched = false;
            for (int attempt = 0; attempt < 15; attempt++)
            {
                Process[] processes = Process.GetProcessesByName(PAGEANT);
                try
                {
                    foreach (Process candidate in processes)
                    {
                        try
                        {
                            candidate.WaitForInputIdle(200);
                            IntPtr handle = candidate.MainWindowHandle;
                            if (handle != IntPtr.Zero)
                                return handle;
                        }
                        catch (InvalidOperationException) { }
                        catch (Win32Exception) { }
                    }
                }
                finally
                {
                    foreach (Process candidate in processes)
                        candidate.Dispose();
                }

                if (!launched && processes.Length == 0)
                {
                    using (var p = Process.Start(new ProcessStartInfo(GetPageantBinaryPath()) { UseShellExecute = true })) { }
                    launched = true;
                }
                Thread.Sleep(100);
            }
            return IntPtr.Zero;
        }

        private static void ShowError(string message, string caption)
        {
            MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        internal static string GetPuttyBinaryPath()
        {
            return ExternalExecutablePaths.Require(PUTTY_BINARY, ExternalExecutablePaths.Putty);
        }

        internal static string GetPageantBinaryPath()
        {
            return ExternalExecutablePaths.Require(PAGEANT_BINARY, ExternalExecutablePaths.Pageant);
        }
    }
}
