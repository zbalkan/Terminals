using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Terminals.Common.Configuration;
using Terminals.Common.Converters;
using Terminals.Common.Forms;
using Terminals.Data;

namespace Terminals.Connections.ICA
{
    internal sealed class CitrixWorkspaceConnection : Connection
    {
        public override bool LaunchOnly { get { return true; } }

        public override bool Connect()
        {
            throw new NotSupportedException("Citrix Workspace is launch-only. Use LaunchAsync.");
        }

        public override async Task LaunchAsync()
        {
            var options = Favorite == null ? null : Favorite.ProtocolProperties as CitrixWorkspaceOptions;
            if (options == null)
                throw new InvalidOperationException("Citrix Workspace options are missing.");

            Uri store = UrlConverter.TryParseHttpsUrl(UrlConverter.ExtractAbsoluteUrl(Favorite));
            if (store == null || !string.IsNullOrEmpty(store.Fragment))
                throw new ArgumentException("A valid HTTPS Citrix store/discovery URL is required.");

            ProcessStartInfo start = BuildStartInfo(options, store);
            await ExternalProcessLauncher.ExecuteAsync(start, false).ConfigureAwait(false);
            // This confirms process creation only; Workspace owns the session lifecycle.
        }

        internal static ProcessStartInfo BuildStartInfo(CitrixWorkspaceOptions options, Uri store)
        {
            if (options == null) throw new ArgumentNullException("options");
            if (store == null || store.Scheme != Uri.UriSchemeHttps ||
                string.IsNullOrEmpty(store.Host) || !string.IsNullOrEmpty(store.UserInfo) ||
                !string.IsNullOrEmpty(store.Fragment))
                throw new ArgumentException("The Citrix store URL must use HTTPS.", "store");

            string resource = (options.ResourceName ?? string.Empty).Trim();
            if (resource.Length == 0 || resource.Length > 512)
                throw new ArgumentException("A valid published resource name is required.");
            foreach (char character in resource)
                if (char.IsControl(character))
                    throw new ArgumentException("Published resource names cannot contain control characters.");

            string file;
            string arguments;
            if (options.LaunchMode == "Cloud")
            {
                file = ExternalExecutablePaths.Require("SelfService.exe", ExternalExecutablePaths.CitrixSelfService);
                arguments = "storebrowse -q " + Quote(resource) + " " + Quote(store.AbsoluteUri);
            }
            else if (options.LaunchMode == "StoreFront")
            {
                file = ExternalExecutablePaths.Require("storebrowse.exe", ExternalExecutablePaths.CitrixStorebrowse);
                arguments = "-S " + Quote(resource) + " " + Quote(store.AbsoluteUri);
            }
            else
                throw new ArgumentException("Unknown Citrix Workspace launch mode.");

            return new ProcessStartInfo
            {
                FileName = file,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(file)
            };
        }

        // Quote one Windows argv entry without allowing an option or command to be injected.
        private static string Quote(string value)
        {
            var quoted = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"')
                {
                    quoted.Append('\\', slashes * 2 + 1);
                    quoted.Append('"');
                    slashes = 0;
                    continue;
                }
                quoted.Append('\\', slashes);
                slashes = 0;
                quoted.Append(c);
            }
            quoted.Append('\\', slashes * 2);
            quoted.Append('"');
            return quoted.ToString();
        }
    }
}
