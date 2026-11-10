using System;
using System.IO;
namespace Terminals.Common.Configuration
{
    public enum SshClientKind { PuTTY, WindowsOpenSSH }
    public static class SshClientPreference
    {
        public static SshClientKind Selected { get; set; } = SshClientKind.PuTTY;
        public static SshClientKind Parse(string value)
        {
            SshClientKind result;
            return Enum.TryParse(value, true, out result) && Enum.IsDefined(typeof(SshClientKind), result)
                ? result : SshClientKind.PuTTY;
        }
        public static string FindWindowsOpenSsh()
        {
            // SpecialFolder.System resolves to the Windows system directory
            // (normally C:\\Windows\\System32), not the Windows installation root.
            string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (string.IsNullOrEmpty(systemDirectory))
                return null;
            string sshPath = Path.Combine(systemDirectory, "OpenSSH", "ssh.exe");
            if (File.Exists(sshPath))
                return sshPath;
            // A 32-bit Terminals process on 64-bit Windows can need Sysnative to
            // bypass WOW64 filesystem redirection when locating the native SSH client.
            if (!Environment.Is64BitProcess && Environment.Is64BitOperatingSystem)
            {
                string windowsDirectory = Environment.GetEnvironmentVariable("WINDIR");
                if (!string.IsNullOrEmpty(windowsDirectory))
                {
                    string nativeSshPath = Path.Combine(windowsDirectory, "Sysnative", "OpenSSH", "ssh.exe");
                    if (File.Exists(nativeSshPath))
                        return nativeSshPath;
                }
            }
            return null;
        }
    }
}
