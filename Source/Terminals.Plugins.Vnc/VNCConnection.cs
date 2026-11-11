using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Terminals.Common.Configuration;
using Terminals.Common.Forms;
using Terminals.Data;

namespace Terminals.Connections
{
    // External TightVNC Viewer owns its window, authentication and session.
    internal sealed class VNCConnection : Connection
    {
        public override bool LaunchOnly { get { return true; } }

        public override bool Connect()
        {
            throw new NotSupportedException("The external VNC viewer is launch-only. Use LaunchAsync.");
        }

        public override async Task LaunchAsync()
        {
            if (Favorite == null)
                throw new InvalidOperationException("A VNC favorite is required.");
            var options = Favorite.ProtocolProperties as VncOptions;
            var start = BuildStartInfo(Favorite.ServerName, Favorite.Port, options);
            await ExternalProcessLauncher.ExecuteAsync(start, false).ConfigureAwait(false);
            // Process creation does not establish VNC authentication or connectivity.
        }

        internal static ProcessStartInfo BuildStartInfo(string server, int port, VncOptions options)
        {
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException("port", "VNC port must be between 1 and 65535.");
            if (string.IsNullOrWhiteSpace(server))
                throw new ArgumentException("A VNC hostname or IP address is required.", "server");

            string host = server.Trim();
            if (host.StartsWith("[", StringComparison.Ordinal) &&
                host.EndsWith("]", StringComparison.Ordinal))
                host = host.Substring(1, host.Length - 2);

            if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
                throw new ArgumentException("Invalid VNC hostname or IP address.", "server");
            if (host.IndexOf(':') >= 0)
                host = "[" + host + "]";

            string viewer = ExternalVncViewers.Selected;
            string executable = ExternalVncViewers.Require(viewer);
            bool viewOnly = options != null && options.ViewOnly;
            bool autoScale = options != null && options.AutoScale;
            string target = "\"" + host + "::" + port + "\"";
            string arguments;
            switch (viewer)
            {
                case ExternalVncViewers.TightVnc:
                    arguments = (viewOnly ? "-viewonly=yes " : "") +
                        (autoScale ? "-scale=auto " : "") + target;
                    break;
                case ExternalVncViewers.UltraVnc:
                    arguments = (viewOnly ? "-viewonly " : "") +
                        (autoScale ? "-autoscaling " : "") + "-connect " + target;
                    break;
                case ExternalVncViewers.TigerVnc:
                    // TigerVNC does not document the TightVNC auto-scale switch.
                    arguments = (viewOnly ? "-ViewOnly " : "") + target;
                    break;
                case ExternalVncViewers.RealVnc:
                    arguments = (viewOnly ? "-SendKeyEvents=FALSE -SendPointerEvents=FALSE " : "") +
                        (autoScale ? "-Scaling=FitAutoAspect " : "") + target;
                    break;
                default:
                    throw new ArgumentException("Unsupported VNC viewer.", "viewer");
            }
            // Never transfer favorite credentials via command line or environment.
            return new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executable)
            };
        }
    }
}
