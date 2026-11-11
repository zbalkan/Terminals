using System;
using System.Windows.Forms;
using Terminals.Common.Network;

namespace Terminals.Network.WMI
{
    internal static class RemoteWmiAvailability
    {
        private const RemoteProtocolStatusCache.Protocol Protocol =
            RemoteProtocolStatusCache.Protocol.WmiDcom;

        internal static bool AllowAttempt(string host, IWin32Window owner)
        {
            RemoteProtocolStatusCache.Failure failure;
            if (!RemoteProtocolStatusCache.TryGetFailure(host, Protocol, out failure))
                return true;
            string message = "A previous WMI/DCOM connection to " + host +
                " failed at the transport layer. Further attempts are suppressed until " +
                failure.ExpiresAt.LocalDateTime.ToString("T") + ".\n\nRetry now?";
            if (MessageBox.Show(owner, message, "Remote administration unavailable",
                MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) != DialogResult.Retry)
                return false;
            RemoteProtocolStatusCache.Retry(host, Protocol);
            return true;
        }

        internal static void Succeeded(string host)
        {
            RemoteProtocolStatusCache.ReportSuccess(host, Protocol);
        }

        internal static void Failed(string host, Exception error)
        {
            RemoteProtocolStatusCache.ReportFailure(host, Protocol, error);
        }
    }
}
