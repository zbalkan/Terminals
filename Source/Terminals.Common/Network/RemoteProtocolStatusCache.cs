using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Terminals.Common.Network
{
    /// <summary>Session-only, passive suppression of remote transport failures.</summary>
    public static class RemoteProtocolStatusCache
    {
        public enum Protocol { WmiDcom, WsManHttp, WsManHttps }
        public sealed class Failure
        {
            public string Reason { get; internal set; }
            public DateTimeOffset ExpiresAt { get; internal set; }
        }
        private static readonly ConcurrentDictionary<string, Failure> Entries =
            new ConcurrentDictionary<string, Failure>(StringComparer.OrdinalIgnoreCase);

        private static string Key(string host, Protocol protocol)
        {
            if (string.IsNullOrWhiteSpace(host)) return null;
            string name = host.Trim().TrimStart('\\');
            int slash = name.IndexOf('\\');
            if (slash >= 0) name = name.Substring(0, slash);
            name = name.TrimEnd('.');
            if (name.Length == 0 || name == "." ||
                name.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                name.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                return null;
            return protocol + ":" + name;
        }

        public static bool TryGetFailure(string host, Protocol protocol, out Failure failure)
        {
            failure = null;
            string key = Key(host, protocol);
            if (key == null) return false;
            Failure found;
            if (!Entries.TryGetValue(key, out found)) return false;
            if (found.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                Failure removed;
                Entries.TryRemove(key, out removed);
                return false;
            }
            failure = found;
            return true;
        }

        public static void Retry(string host, Protocol protocol)
        {
            string key = Key(host, protocol);
            if (key != null)
            {
                Failure removed;
                Entries.TryRemove(key, out removed);
            }
        }

        public static void ReportSuccess(string host, Protocol protocol)
        {
            Retry(host, protocol);
        }

        public static bool ReportFailure(string host, Protocol protocol, Exception exception)
        {
            if (!IsTransportFailure(exception)) return false;
            string key = Key(host, protocol);
            if (key == null) return false;
            Entries[key] = new Failure
            {
                Reason = "Remote " + protocol + " transport failure (" + exception.GetType().Name + ")",
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(60)
            };
            return true;
        }

        public static bool IsTransportFailure(Exception exception)
        {
            for (Exception e = exception; e != null; e = e.InnerException)
            {
                var socket = e as SocketException;
                if (socket != null &&
                    (socket.SocketErrorCode == SocketError.TimedOut ||
                     socket.SocketErrorCode == SocketError.ConnectionRefused ||
                     socket.SocketErrorCode == SocketError.HostUnreachable ||
                     socket.SocketErrorCode == SocketError.NetworkUnreachable))
                    return true;
                var com = e as COMException;
                if (com != null)
                    switch (unchecked((uint)com.ErrorCode))
                    {
                        case 0x800706BA:
                        case 0x80041015: // WMI transport failure
                        case 0x80070035:
                        case 0x8007274C:
                        case 0x8007274D:
                            return true;
                    }
                if (e is TimeoutException) return true;
            }
            return false;
        }
    }
}
