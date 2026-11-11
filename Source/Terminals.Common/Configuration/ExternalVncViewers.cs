using System;
using System.Diagnostics;
using System.IO;

namespace Terminals.Common.Configuration
{
    // A vncviewer.exe file name is shared by several incompatible VNC clients.
    // Never treat a matching PATH filename alone as proof of the selected vendor.
    public static class ExternalVncViewers
    {
        public const string TightVnc = "TightVNC";
        public const string UltraVnc = "UltraVNC";
        public const string TigerVnc = "TigerVNC";
        public const string RealVnc = "RealVNC";

        public static string Selected { get; set; } = TightVnc;
        public static string UltraVncPath { get; set; }
        public static string TigerVncPath { get; set; }
        public static string RealVncPath { get; set; }

        public static bool IsSupported(string vendor)
        {
            return vendor == TightVnc || vendor == UltraVnc ||
                   vendor == TigerVnc || vendor == RealVnc;
        }

        public static string ExecutableName(string vendor)
        {
            if (vendor == TightVnc) return "tvnviewer.exe";
            if (IsSupported(vendor)) return "vncviewer.exe";
            throw new ArgumentException("Unsupported VNC viewer: " + vendor, "vendor");
        }

        public static string ConfiguredPath(string vendor)
        {
            switch (vendor)
            {
                case TightVnc: return ExternalExecutablePaths.VncViewer;
                case UltraVnc: return UltraVncPath;
                case TigerVnc: return TigerVncPath;
                case RealVnc: return RealVncPath;
                default: throw new ArgumentException("Unsupported VNC viewer.", "vendor");
            }
        }

        public static string Find(string vendor, string configured)
        {
            if (!IsSupported(vendor)) throw new ArgumentException("Unsupported VNC viewer.", "vendor");
            string name = ExecutableName(vendor);
            if (!string.IsNullOrWhiteSpace(configured))
                return ExternalExecutablePaths.Find(name, configured);

            // Prefer vendor-specific installed locations. Do not search the
            // entire filesystem, and never silently substitute another VNC product.
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string[] relative = vendor == TightVnc ? new[] { @"TightVNC\tvnviewer.exe" } :
                vendor == UltraVnc ? new[] { @"UltraVNC\vncviewer.exe" } :
                vendor == TigerVnc ? new[] { @"TigerVNC\vncviewer.exe", @"TigerVNC\vncviewer64.exe" } :
                new[] { @"RealVNC\VNC Viewer\vncviewer.exe", @"VNC Viewer\vncviewer.exe" };

            foreach (string root in new[] { programFiles, programFilesX86 })
                if (!string.IsNullOrWhiteSpace(root))
                    foreach (string suffix in relative)
                    {
                        string candidate = Path.Combine(root, suffix);
                        if (File.Exists(candidate)) return candidate;
                    }

            // PATH discovery is allowed only if file metadata identifies the
            // requested viewer. Portable builds with no metadata require a path.
            foreach (string filename in vendor == TigerVnc
                ? new[] { "vncviewer.exe", "vncviewer64.exe" } :
                new[] { name })
            {
                string candidate = ExternalExecutablePaths.Find(filename, null);
                if (candidate != null && MatchesVendor(candidate, vendor))
                    return candidate;
            }
            return null;
        }

        private static bool MatchesVendor(string path, string vendor)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                string identity = ((info.ProductName ?? "") + " " +
                                   (info.CompanyName ?? "")).ToLowerInvariant();
                switch (vendor)
                {
                    case TightVnc: return identity.Contains("tightvnc");
                    case UltraVnc: return identity.Contains("ultravnc");
                    case TigerVnc: return identity.Contains("tigervnc");
                    case RealVnc: return identity.Contains("realvnc");
                    default: return false;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException ||
                                       ex is System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        public static string Require(string vendor)
        {
            string path = Find(vendor, ConfiguredPath(vendor));
            if (path == null)
                throw new FileNotFoundException(vendor + " viewer was not found. " +
                    "Choose an installed viewer or configure its full executable path under Options > Plugins.");
            return path;
        }
    }
}
