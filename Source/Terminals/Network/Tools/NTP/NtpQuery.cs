using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Terminals.Network.NTP
{
    // A bounded SNTP query. It reads remote time but never changes the system clock.
    internal static class NtpQuery
    {
        internal const int TimeoutMilliseconds = 4000;
        private const int PacketLength = 48;
        private const ulong NtpEpochTicks = 599266080000000000UL;

        internal static string NormalizeHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return Unified.Network.SNTP.NTPClient.DefaultTimeServer;
            host = host.Trim();
            if (host.Length > 253 || host.IndexOfAny(new[] { '/', '\\', ' ', '\t', '\r', '\n' }) >= 0)
                throw new ArgumentException("Enter a valid NTP server hostname or IP address.", nameof(host));
            return host;
        }

        private static byte[] CreateRequest(DateTime utcNow)
        {
            var packet = new byte[PacketLength];
            packet[0] = 0x23; // LI=0, SNTP v4, client mode=3.
            WriteTimestamp(packet, 40, utcNow);
            return packet;
        }

        private static void WriteTimestamp(byte[] packet, int offset, DateTime utc)
        {
            ulong ticks = (ulong)utc.ToUniversalTime().Ticks - NtpEpochTicks;
            ulong seconds = ticks / (ulong)TimeSpan.TicksPerSecond;
            ulong fraction = (ticks % (ulong)TimeSpan.TicksPerSecond) * 0x100000000UL /
                (ulong)TimeSpan.TicksPerSecond;
            for (int i = 3; i >= 0; --i)
            {
                packet[offset + i] = (byte)seconds;
                seconds >>= 8;
                packet[offset + 4 + i] = (byte)fraction;
                fraction >>= 8;
            }
        }

        private static DateTime ReadTimestamp(byte[] packet, int offset)
        {
            ulong seconds = 0;
            ulong fraction = 0;
            for (int i = 0; i < 4; i++)
            {
                seconds = (seconds << 8) | packet[offset + i];
                fraction = (fraction << 8) | packet[offset + 4 + i];
            }
            if (seconds == 0 && fraction == 0)
                throw new InvalidOperationException("NTP response contains an empty timestamp.");
            ulong ticks = NtpEpochTicks + seconds * (ulong)TimeSpan.TicksPerSecond +
                fraction * (ulong)TimeSpan.TicksPerSecond / 0x100000000UL;
            return new DateTime(checked((long)ticks), DateTimeKind.Utc);
        }

        internal static NtpResult ParseResponse(byte[] request, byte[] response, DateTime receivedUtc)
        {
            if (request == null || request.Length != PacketLength)
                throw new ArgumentException("Invalid NTP request.", nameof(request));
            if (response == null || response.Length < PacketLength)
                throw new InvalidOperationException("NTP reply is shorter than 48 bytes.");
            int mode = response[0] & 7;
            int version = (response[0] >> 3) & 7;
            int leap = response[0] >> 6;
            int stratum = response[1];
            if (mode != 4 || version < 3 || version > 4 || leap == 3 || stratum < 1 || stratum > 15)
                throw new InvalidOperationException("The NTP server returned an invalid or unsynchronized response.");

            for (int i = 0; i < 8; ++i)
                if (request[40 + i] != response[24 + i])
                    throw new InvalidOperationException("NTP originate timestamp does not match the request.");

            DateTime transmit = ReadTimestamp(response, 40);
            DateTime receive = ReadTimestamp(response, 32);
            DateTime sent = ReadTimestamp(request, 40);
            double delay = (receivedUtc - sent).TotalMilliseconds - (transmit - receive).TotalMilliseconds;
            double offset = ((receive - sent).TotalMilliseconds +
                (transmit - receivedUtc).TotalMilliseconds) / 2;
            return new NtpResult
            {
                ServerTimeUtc = transmit,
                ReceivedAtUtc = receivedUtc,
                Stratum = stratum,
                RoundTripDelayMilliseconds = Math.Max(0, delay),
                ClockOffsetMilliseconds = offset,
                Version = version
            };
        }

        internal static async Task<NtpResult> GetAsync(string host, CancellationToken cancellation)
        {
            host = NormalizeHost(host);
            cancellation.ThrowIfCancellationRequested();
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                deadline.CancelAfter(TimeoutMilliseconds);
                try
                {
                    // DNS APIs on .NET Framework do not accept cancellation; bound the wait.
                    Task<IPAddress[]> dns = Dns.GetHostAddressesAsync(host);
                    IPAddress[] addresses = await WithDeadline(dns, deadline.Token);
                    if (addresses.Length == 0)
                        throw new InvalidOperationException("NTP hostname resolved to no addresses.");

                    IPAddress endpoint = Array.Find(addresses, address =>
                        address.AddressFamily == AddressFamily.InterNetwork ||
                        address.AddressFamily == AddressFamily.InterNetworkV6);
                    if (endpoint == null)
                        throw new InvalidOperationException("No supported NTP server address found.");

                    using (var udp = new UdpClient(endpoint.AddressFamily))
                    using (deadline.Token.Register(() => udp.Close()))
                    {
                        udp.Connect(endpoint, 123);
                        DateTime sentAt = DateTime.UtcNow;
                        byte[] request = CreateRequest(sentAt);
                        await WithDeadline(udp.SendAsync(request, request.Length), deadline.Token);
                        UdpReceiveResult reply = await WithDeadline(udp.ReceiveAsync(), deadline.Token);
                        cancellation.ThrowIfCancellationRequested();
                        return ParseResponse(request, reply.Buffer, DateTime.UtcNow);
                    }
                }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                {
                    throw new TimeoutException("NTP request exceeded the four-second deadline.");
                }
                catch (ObjectDisposedException) when (deadline.IsCancellationRequested)
                {
                    cancellation.ThrowIfCancellationRequested();
                    throw new TimeoutException("NTP request exceeded the four-second deadline.");
                }
                catch (SocketException) when (deadline.IsCancellationRequested)
                {
                    cancellation.ThrowIfCancellationRequested();
                    throw new TimeoutException("NTP request exceeded the four-second deadline.");
                }
            }
        }

        private static async Task<T> WithDeadline<T>(Task<T> work, CancellationToken cancellation)
        {
            Task timed = Task.Delay(Timeout.Infinite, cancellation);
            if (await Task.WhenAny(work, timed) != work)
            {
                cancellation.ThrowIfCancellationRequested();
                throw new TimeoutException("NTP operation timed out.");
            }
            cancellation.ThrowIfCancellationRequested();
            return await work;
        }
    }

    // A safe, eagerly materialized PropertyGrid model; no getter initiates network I/O.
    internal sealed class NtpResult
    {
        [DisplayName("Server UTC time")]
        public DateTime ServerTimeUtc { get; internal set; }
        [DisplayName("Local reception UTC time")]
        public DateTime ReceivedAtUtc { get; internal set; }
        public int Stratum { get; internal set; }
        public int Version { get; internal set; }
        [DisplayName("Round trip delay (ms)")]
        public double RoundTripDelayMilliseconds { get; internal set; }
        [DisplayName("Local clock offset (ms)")]
        public double ClockOffsetMilliseconds { get; internal set; }
    }
}
