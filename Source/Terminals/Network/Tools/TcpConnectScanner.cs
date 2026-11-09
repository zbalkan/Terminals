using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Terminals.Network
{
    internal static class TcpConnectScanner
    {
        internal static async Task<bool> IsOpenAsync(
            IPAddress address, int port, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            cancellationToken.ThrowIfCancellationRequested();
            using (var client = new TcpClient(address.AddressFamily))
            {
                try
                {
                    Task connect = client.ConnectAsync(address, port);
                    Task timeoutTask = Task.Delay(timeout, cancellationToken);
                    Task completed = await Task.WhenAny(connect, timeoutTask).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (completed != connect)
                        return false;
                    await connect.ConfigureAwait(false);
                    return client.Connected;
                }
                catch (SocketException)
                {
                    return false;
                }
            }
        }
    }
}
