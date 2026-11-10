using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Terminals.Common.Configuration;
using Terminals.Connections;

namespace Terminals.Plugins.Putty
{
    /// <summary>Launches ssh.exe in an independent console; ConPTY is not implemented.</summary>
    internal sealed class WindowsOpenSshConnection : Connection
    {
        private Process process;

        public override bool Connected { get { return process != null && !process.HasExited; } }

        public WindowsOpenSshConnection()
        {
            Controls.Add(new Label
            {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                Text = "Windows OpenSSH opens in a separate console window.\r\n" +
                       "Authentication and host-key verification are managed by OpenSSH."
            });
        }

        public override bool Connect()
        {
            string executable = SshClientPreference.FindWindowsOpenSsh();
            if (executable == null)
            {
                LastError = "Windows OpenSSH Client is not installed. Install it under Windows Optional Features or select PuTTY.";
                MessageBox.Show(LastError, "SSH", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            try
            {
                var credentials = ResolveFavoriteCredentials();
                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = executable,
                        Arguments = BuildArguments(Favorite.ServerName, Favorite.Port, credentials.UserName),
                        UseShellExecute = true, WindowStyle = ProcessWindowStyle.Normal
                    },
                    EnableRaisingEvents = true
                };
                process.Exited += (sender, args) => FireDisconnected();
                if (!process.Start())
                {
                    LastError = "Unable to start Windows OpenSSH.";
                    process.Dispose();
                    process = null;
                    return false;
                }
                return true;
            }
            catch (Exception ex) when (ex is Win32Exception || ex is IOException ||
                                       ex is InvalidOperationException || ex is ArgumentException)
            {
                LastError = "Unable to launch Windows OpenSSH: " + ex.Message;
                MessageBox.Show(LastError, "SSH", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (process != null) { process.Dispose(); process = null; }
                return false;
            }
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
            // Escape only backslashes before quotation marks or the closing
            // delimiter. Doubling every backslash corrupts DOMAIN\user.
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
            if (disposing && process != null)
            {
                try { if (!process.HasExited) process.Kill(); }
                catch (InvalidOperationException) { }
                catch (Win32Exception) { }
                finally { process.Dispose(); process = null; }
            }
            base.Dispose(disposing);
        }
    }
}
