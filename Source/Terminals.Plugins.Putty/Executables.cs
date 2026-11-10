using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            try
            {
                ExternalLinks.OpenPath(GetPuttyBinaryPath());
            }
            catch (FileNotFoundException ex)
            {
                MessageBox.Show(ex.Message, PUTTY_BINARY, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        internal static void LaunchPageant()
        {
            try
            {
                Process[] processes = EnsurePageantProcessRunning();
                if (processes.Any())
                    ShowEditCertificatesWindow(processes[0]);
                else
                    MessageBox.Show(Properties.Resources.PageantUnableToStart, PAGEANT_BINARY, MessageBoxButtons.OK);
            }
            catch (FileNotFoundException ex)
            {
                MessageBox.Show(ex.Message, PAGEANT_BINARY, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static Process[] EnsurePageantProcessRunning()
        {
            var processes = Process.GetProcessesByName(PAGEANT);
            if (processes.Any())
                return processes;

            ExternalLinks.OpenPath(GetPageantBinaryPath());
            return Process.GetProcessesByName(PAGEANT);
        }

        private static void ShowEditCertificatesWindow(Process pageantProcess)
        {
            pageantProcess.WaitForInputIdle(5000);
            IntPtr handle = pageantProcess.MainWindowHandle;
            if (handle == IntPtr.Zero)
            {
                MessageBox.Show(Properties.Resources.PageantMessage, PAGEANT_BINARY, MessageBoxButtons.OK);
                return;
            }

            Methods.ShowWindowAsync(handle, (int)ShowWindowCommands.ShowDefault);
            Methods.ShowWindowAsync(handle, (int)ShowWindowCommands.Show);
            Methods.SetForegroundWindow(handle);
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
