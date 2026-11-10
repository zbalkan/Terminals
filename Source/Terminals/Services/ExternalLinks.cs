using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using Terminals.Common.Forms;
using Terminals.Configuration;
using Terminals.Connections;
using Terminals.Data;
using Terminals.Forms;
using Terminals.Updates;

namespace Terminals.Services
{
    internal class ExternalLinks : Common.Forms.ExternalLinks
    {
        internal static string TerminalsReleasesUrl
        {
            get { return Program.Resources.GetString("TerminalsURL"); }
        }

        internal static void ShowWinPCapPage()
        {
            const string MESSAGE = "It appears that WinPcap is not installed.  In order to use this feature within Terminals you must first install that product.  Do you wish to visit the download location right now?";
            if (MessageBox.Show(MESSAGE, "Download WinPcap?", MessageBoxButtons.OKCancel) == DialogResult.OK)
            {
                OpenPath("http://www.winpcap.org/install/default.htm");
            }
        }

        internal static void AskIfShowReleasePage(Settings settings, ReleaseInfo releaseInfo)
        {
            string message = string.Format("Version:{0}\r\nPublished:{1}\r\nDo you want to show the Terminals home page?",
                                            releaseInfo.Version, releaseInfo.Published);
            YesNoDisableResult answer = YesNoDisableForm.ShowDialog("New release is available", message);
            if (answer.Result == DialogResult.Yes)
                ShowReleasePage();

            if (answer.Disable)
                settings.NeverShowTerminalsWindow = true;
        }

        internal static void ShowReleasePage()
        {
            OpenPath(TerminalsReleasesUrl);
        }

        internal static void OpenAuthorPage()
        {
            OpenPath("http://weblogs.asp.net/rchartier/");
        }

        internal static void OpenLogsFolder()
        {
            OpenPath(FileLocations.LogDirectory);
        }

        internal static Task CallExecuteBeforeConnectedAsync(IBeforeConnectExecuteOptions executeOptions)
        {
            if (!executeOptions.Execute || string.IsNullOrEmpty(executeOptions.Command))
                return Task.FromResult(0);

            var start = new ProcessStartInfo(executeOptions.Command, executeOptions.CommandArguments);
            start.WorkingDirectory = executeOptions.InitialDirectory;
            return ExternalProcessLauncher.ExecuteAsync(start, executeOptions.WaitForExit);
        }

        internal static void Launch(SpecialCommandConfigurationElement command)
        {
            try
            {
                TryLaunch(command);
            }
            catch (Exception ex)
            {
                string message = String.Format("Could not Launch the shortcut application: '{0}'", command.Name);
                MessageBox.Show(message);
                Logging.Error(message, ex);
            }
        }

        private static void TryLaunch(SpecialCommandConfigurationElement command)
        {
            string exe = command.Executable;
            if (exe.Contains("%"))
                exe = exe.Replace("%systemroot%", Environment.GetEnvironmentVariable("systemroot"));

            var startInfo = new ProcessStartInfo(exe, command.Arguments);
            startInfo.WorkingDirectory = command.WorkingFolder;
            ExternalProcessLauncher.StartDetached(startInfo,
                ex => Logging.Error("Unable to launch shortcut application.", ex),
                SynchronizationContext.Current);
        }

        internal static void StartMsManagementConsole(IFavorite favorite)
        {
            if (favorite != null)
                ExternalProcessLauncher.StartDetached(new ProcessStartInfo("mmc.exe", "compmgmt.msc /a /computer=" + favorite.ServerName),
                    ex => Logging.Error("Unable to launch computer management.", ex), SynchronizationContext.Current);
        }

        internal static void OpenLocalComputerManagement()
        {
            ExternalProcessLauncher.StartDetached(new ProcessStartInfo("mmc.exe", "compmgmt.msc /a /computer=."),
                ex => Logging.Error("Unable to launch local computer management.", ex), SynchronizationContext.Current);
        }

        internal static void StartMsInfo32(IFavorite favorite)
        {
            if (favorite != null)
            {
                String programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                //if(programFiles.Contains("(x86)")) programFiles = programFiles.Replace(" (x86)","");
                String path = String.Format(@"{0}\common files\Microsoft Shared\MSInfo\msinfo32.exe", programFiles);
                if (File.Exists(path))
                {
                    ExternalProcessLauncher.StartDetached(new ProcessStartInfo(path, String.Format("/computer {0}", favorite.ServerName)),
                        ex => Logging.Error("Unable to launch MSInfo32.", ex), SynchronizationContext.Current);
                }
            }
        }

        internal static void OpenFileInNotepad(string file)
        {
            if (MessageBox.Show("Open dump file in notepad?") == DialogResult.OK)
            {
                ExternalProcessLauncher.StartDetached(new ProcessStartInfo("notepad.exe", "\"" + file + "\""),
                    ex => Logging.Error("Unable to launch Notepad.", ex), SynchronizationContext.Current);
            }
        }

        internal static void OpenTerminalServiceCommandPrompt(IConnectionExtra terminal, string psexecLocation)
        {
            // Reading the remote session through TSManager can require a network
            // roundtrip. Perform discovery and process creation on the same worker.
            var context = SynchronizationContext.Current;
            ExternalProcessLauncher.RunAsync(() =>
            {
                try
                {
                    string sessionId = String.Empty;
                    if (!terminal.ConnectToConsole)
                    {
                        sessionId = TSManager.GetCurrentSession(terminal.Server,
                            terminal.UserName, terminal.Domain, Environment.MachineName).Id.ToString();
                    }

                    string args = String.Format(" \\\\{0} -i {1} -d cmd", terminal.Server, sessionId);
                    using (var process = new Process())
                    {
                        process.StartInfo = new ProcessStartInfo(psexecLocation, args)
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        if (!process.Start())
                            throw new InvalidOperationException("PsExec did not start.");
                    }
                }
                catch (Exception ex)
                {
                    ExternalProcessLauncher.Post(context,
                        () => Logging.Error("Unable to launch terminal service command prompt.", ex));
                }
            });
        }
    }
}
