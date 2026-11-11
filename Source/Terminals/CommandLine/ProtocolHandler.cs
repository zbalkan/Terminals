using System;
using Microsoft.Win32;
using System.Windows.Forms;

namespace Terminals
{
    internal class ProtocolHandler
    {
        private const string TRM_REGISTRY = "TRM";

        internal static void Register()
        {
            try
            {
                // HKCR merges HKLM and HKCU; ignore stale machine-level MSI registrations.
                if (!IsTrmKeyRegistred())
                    CreateTrmRegistrySubKey();
            }
            catch(Exception ex)
            {
                //ignore any security errors and such
                Logging.Error("Error accessing registry", ex);
            }
        }

        private static void CreateTrmRegistrySubKey()
        {
            using (RegistryKey trmKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + TRM_REGISTRY))
            {
                trmKey.SetValue(null, "URL:Terminals Protocol");
                trmKey.SetValue("URL Protocol", "");
                using (RegistryKey shellKey = trmKey.CreateSubKey("shell"))
                {
                    shellKey.SetValue(null, "open");
                    using (RegistryKey commandKey = shellKey.CreateSubKey("open\\command"))
                    {
                        commandKey.SetValue(null, BuildOpenCommand(Application.ExecutablePath));
                    }
                }
            }
        }

        // Quoting the executable prevents spaces in Program Files paths from breaking the handler.
        internal static string BuildOpenCommand(string executablePath)
        {
            return "\"" + executablePath + "\" /reuse /url:\"%1\"";
        }

        private static bool IsTrmKeyRegistred()
        {
            using (RegistryKey commandKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Classes\" + TRM_REGISTRY + @"\shell\open\command"))
            {
                if (commandKey == null)
                    return false;

                string command = commandKey.GetValue(null) as string;
                return String.Equals(command, BuildOpenCommand(Application.ExecutablePath),
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void Parse(string url, out string server, out int port)
        {
            server = ParseServerNameByPrefix(url);

            if (server.EndsWith("/"))
                server = server.TrimEnd('/');
            port = 0;
            string[] serverParams = server.Split(':');
            if (serverParams.Length == 2)
            {
                server = serverParams[0];
                port = Int32.Parse(serverParams[1]);
            }
        }

        private static string ParseServerNameByPrefix(string url)
        {
            const string PREFIX = "trm://";

            if (url.Length < (PREFIX).Length)
                return string.Empty;

            return url.Substring((PREFIX).Length);
        }
    }
}
