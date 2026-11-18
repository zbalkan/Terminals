using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Terminals.Network.WhoIs
{
    /// <summary>Asynchronous WHOIS lookup with one referral and a shared resource budget.</summary>
    public static class WhoisQuery
    {
        public const int TimeoutMilliseconds = 12000;
        public const int MaximumResponseBytes = 256 * 1024;
        private const int WhoisPort = 43;

        public static string NormalizeDomain(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Enter a domain or hostname.", nameof(input));
            string domain = input.Trim();
            if (domain.Length > 253 || domain.IndexOfAny(new[] { '\r', '\n', '\0', ' ', '\t' }) >= 0)
                throw new ArgumentException("Invalid WHOIS query.", nameof(input));
            if (domain.StartsWith("=", StringComparison.Ordinal))
                domain = domain.Substring(1);
            if (domain.Length == 0 || domain.StartsWith(".", StringComparison.Ordinal) ||
                domain.EndsWith(".", StringComparison.Ordinal))
                throw new ArgumentException("Invalid WHOIS hostname.", nameof(input));
            foreach (string label in domain.Split('.'))
            {
                if (label.Length == 0 || label.Length > 63)
                    throw new ArgumentException("Invalid WHOIS hostname label.", nameof(input));
                foreach (char character in label)
                    if (!(character >= 'a' && character <= 'z') &&
                        !(character >= 'A' && character <= 'Z') &&
                        !(character >= '0' && character <= '9') && character != '-')
                        throw new ArgumentException("Invalid WHOIS hostname character.", nameof(input));
            }
            if (domain.IndexOf('.') < 0)
                throw new ArgumentException("A WHOIS hostname must contain a suffix.", nameof(input));
            return domain;
        }

        public static string GetDefaultServer(string domain)
        {
            string normalized = NormalizeDomain(domain);
            string suffix = normalized.Substring(normalized.LastIndexOf('.') + 1);
            return suffix.ToLowerInvariant() + ".whois-servers.net";
        }

        public static string FormatQuery(string domain)
        {
            string normalized = NormalizeDomain(domain);
            // Preserve the historical exact-match query except for .ca domains.
            return normalized.EndsWith(".ca", StringComparison.OrdinalIgnoreCase)
                ? normalized : "=" + normalized;
        }

        public static string ParseReferral(string response)
        {
            if (string.IsNullOrEmpty(response)) return null;
            using (var reader = new StringReader(response))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    const string marker = "Whois Server:";
                    if (!line.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
                        continue;
                    string server = line.Substring(marker.Length).Trim();
                    if (IsValidServer(server))
                        return server;
                }
            }
            return null;
        }

        private static bool IsValidServer(string host)
        {
            if (string.IsNullOrEmpty(host) || host.Length > 253 ||
                host.StartsWith(".", StringComparison.Ordinal) ||
                host.EndsWith(".", StringComparison.Ordinal))
                return false;
            // Referral targets are hostnames, not URLs, arbitrary ports, or raw IP addresses.
            foreach (string label in host.Split('.'))
            {
                if (label.Length == 0 || label.Length > 63) return false;
                foreach (char c in label)
                    if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
                        !(c >= '0' && c <= '9') && c != '-')
                        return false;
            }
            IPAddress ip;
            return host.IndexOf('.') > 0 && !IPAddress.TryParse(host, out ip);
        }

        public static async Task<string> LookupAsync(string domain, CancellationToken cancellation)
        {
            string name = NormalizeDomain(domain);
            string primary = GetDefaultServer(name);
            string request = FormatQuery(name);
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                deadline.CancelAfter(TimeoutMilliseconds);
                try
                {
                    string initial = await QueryServerAsync(request, primary, MaximumResponseBytes, deadline.Token);
                    string referral = ParseReferral(initial);
                    if (referral == null || referral.Equals(primary, StringComparison.OrdinalIgnoreCase))
                        return initial;

                    // A second WHOIS server is the only permitted referral. Budget is shared.
                    int remaining = MaximumResponseBytes - Encoding.UTF8.GetByteCount(initial);
                    if (remaining <= 0)
                        throw new InvalidDataException("WHOIS response exceeded the configured limit.");
                    string followUp = await QueryServerAsync(name, referral, remaining, deadline.Token);
                    return initial + Environment.NewLine +
                        "----------------------Sub Query:" + referral +
                        "--------------------------" + Environment.NewLine + followUp;
                }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                {
                    throw new TimeoutException("WHOIS query exceeded its 12-second deadline.");
                }
            }
        }

        private static async Task<string> QueryServerAsync(string query, string host, int byteLimit,
            CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var lookup = Dns.GetHostAddressesAsync(host);
            IPAddress[] addresses = await AwaitOrCancel(lookup, cancellation);
            if (addresses.Length == 0)
                throw new InvalidOperationException("WHOIS server returned no addresses.");

            // Try the first available address without blocking the WinForms thread.
            Exception lastError = null;
            foreach (IPAddress address in addresses)
            {
                cancellation.ThrowIfCancellationRequested();
                if (address.AddressFamily != AddressFamily.InterNetwork &&
                    address.AddressFamily != AddressFamily.InterNetworkV6)
                    continue;
                try
                {
                    using (var client = new TcpClient(address.AddressFamily))
                    using (cancellation.Register(() => client.Close()))
                    {
                        await AwaitOrCancel(client.ConnectAsync(address, WhoisPort), cancellation);
                        using (NetworkStream stream = client.GetStream())
                        {
                            byte[] payload = Encoding.ASCII.GetBytes(query + "\r\n");
                            await AwaitOrCancel(stream.WriteAsync(payload, 0, payload.Length, cancellation), cancellation);
                            return await ReadResponseAsync(stream, byteLimit, cancellation);
                        }
                    }
                }
                catch (Exception exception) when (exception is SocketException ||
                    exception is IOException || exception is ObjectDisposedException)
                {
                    cancellation.ThrowIfCancellationRequested();
                    lastError = exception;
                }
            }
            throw new IOException("Could not connect to the WHOIS server.", lastError);
        }

        // Operates on streams so response limits can be tested without a network service.
        public static async Task<string> ReadResponseAsync(Stream stream, int byteLimit,
            CancellationToken cancellation)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (byteLimit < 1) throw new ArgumentOutOfRangeException(nameof(byteLimit));
            byte[] buffer = new byte[4096];
            using (var result = new MemoryStream())
            {
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int size = await AwaitOrCancel(
                        stream.ReadAsync(buffer, 0, buffer.Length, cancellation), cancellation);
                    if (size == 0) break;
                    if (result.Length + size > byteLimit)
                        throw new InvalidDataException("WHOIS response exceeded the configured limit.");
                    result.Write(buffer, 0, size);
                }
                return Encoding.UTF8.GetString(result.ToArray());
            }
        }

        private static async Task<T> AwaitOrCancel<T>(Task<T> task, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var cancelled = new TaskCompletionSource<bool>();
            using (cancellation.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, cancelled.Task) != task)
                    cancellation.ThrowIfCancellationRequested();
                cancellation.ThrowIfCancellationRequested();
                return await task;
            }
        }

        private static async Task AwaitOrCancel(Task task, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var cancelled = new TaskCompletionSource<bool>();
            using (cancellation.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, cancelled.Task) != task)
                    cancellation.ThrowIfCancellationRequested();
                cancellation.ThrowIfCancellationRequested();
                await task;
            }
        }
    }
}
