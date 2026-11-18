using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Terminals.Network
{
    internal sealed class TraceRoute : IDisposable
    {
        private readonly List<TraceRouteHopData> hops = new List<TraceRouteHopData>();
        internal string Destination { get; set; }
        internal bool ResolveNames { get; set; } = true;
        internal int BufferSize { get; set; } = 32;
        internal int HopLimit { get; set; } = 128;
        internal int HopTimeOut { get; set; } = 5000;
        internal IList<TraceRouteHopData> Hops { get { return hops.AsReadOnly(); } }

        internal static IPAddress SelectDestination(IPAddress[] addresses)
        {
            if (addresses == null || addresses.Length == 0)
                throw new ArgumentException("DNS returned no addresses.", nameof(addresses));
            return addresses[0];
        }

        internal static bool ReachedDestination(IPStatus status, IPAddress actual, IPAddress destination)
        {
            return status == IPStatus.Success && actual != null && actual.Equals(destination);
        }

        internal async Task RunAsync(CancellationToken cancellation, Action<TraceRouteHopData> onHop)
        {
            if (string.IsNullOrWhiteSpace(Destination))
                throw new ArgumentException("A destination is required.", nameof(Destination));
            if (HopLimit < 1 || HopLimit > 128)
                throw new ArgumentOutOfRangeException(nameof(HopLimit));
            if (HopTimeOut < 100 || HopTimeOut > 10000)
                throw new ArgumentOutOfRangeException(nameof(HopTimeOut));
            if (BufferSize < 1 || BufferSize > 65500)
                throw new ArgumentOutOfRangeException(nameof(BufferSize));

            hops.Clear();
            cancellation.ThrowIfCancellationRequested();
            IPAddress destination = SelectDestination(await Dns.GetHostAddressesAsync(Destination));
            cancellation.ThrowIfCancellationRequested();
            byte[] payload = Encoding.ASCII.GetBytes(new string('.', BufferSize));

            for (int ttl = 1; ttl <= HopLimit; ttl++)
            {
                cancellation.ThrowIfCancellationRequested();
                PingReply reply;
                using (var ping = new System.Net.NetworkInformation.Ping())
                using (cancellation.Register(() => ping.Dispose()))
                {
                    try
                    {
                        reply = await ping.SendPingAsync(destination, HopTimeOut, payload,
                            new PingOptions(ttl, true));
                    }
                    catch (Exception) when (cancellation.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellation);
                    }
                }

                cancellation.ThrowIfCancellationRequested();
                IPAddress address = reply.Address;
                IPStatus status = reply.Status;
                // The original TTL probe supplies its own RTT; no secondary ping.
                var hop = new TraceRouteHopData((byte)ttl, address, reply.RoundtripTime, status);
                if (ResolveNames && address != null &&
                    (status == IPStatus.Success || status == IPStatus.TtlExpired))
                {
                    try
                    {
                        // A slow reverse lookup must not stall the trace indefinitely.
                        Task<IPHostEntry> lookup = Dns.GetHostEntryAsync(address);
                        Task timeout = Task.Delay(Math.Min(HopTimeOut, 1500), cancellation);
                        if (await Task.WhenAny(lookup, timeout) == lookup)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            hop.HostName = (await lookup).HostName;
                        }
                        cancellation.ThrowIfCancellationRequested();
                    }
                    catch (System.Net.Sockets.SocketException) { }
                }

                cancellation.ThrowIfCancellationRequested();
                hops.Add(hop);
                if (onHop != null) onHop(hop);
                if (ReachedDestination(status, address, destination))
                    break;
            }
        }

        public void Dispose() { }
    }
}
