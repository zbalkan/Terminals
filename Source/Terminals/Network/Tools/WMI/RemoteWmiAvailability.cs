using System;
using System.Windows.Forms;
using System.Management;
using System.Runtime.InteropServices;
using Terminals.Common.Network;

namespace Terminals.Network.WMI
{
    internal static class RemoteWmiAvailability
    {
        private const RemoteProtocolStatusCache.Protocol Protocol =
            RemoteProtocolStatusCache.Protocol.WmiDcom;

        // Existing WMI consumers fall back to local WMI unless all three
        // remote connection fields are populated. Do not cache a remote
        // endpoint when the actual query runs against the local machine.
        internal static string EffectiveHost(string host, string username, string password)
        {
            return string.IsNullOrWhiteSpace(host) ||
                   string.IsNullOrWhiteSpace(username) ||
                   string.IsNullOrWhiteSpace(password) ? null : host;
        }

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
            // ManagementException may wrap WBEM_E_TRANSPORT_FAILURE without
            // exposing the underlying COMException. Do not suppress access
            // denied, invalid classes, namespaces or provider-level errors.
            var management = error as ManagementException;
            if (management != null && management.ErrorCode == ManagementStatus.TransportFailure)
            {
                RemoteProtocolStatusCache.ReportFailure(host, Protocol,
                    new COMException("WMI transport failure", unchecked((int)0x80041015)));
                return;
            }
            RemoteProtocolStatusCache.ReportFailure(host, Protocol, error);
        }
    }
}
