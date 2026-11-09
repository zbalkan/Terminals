using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Network;

namespace Tests.Network
{
    [TestClass]
    public class TcpConnectScannerTests
    {
        [TestMethod]
        public void ListenerReportsOpen()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                bool open = TcpConnectScanner.IsOpenAsync(
                    IPAddress.Loopback, port, TimeSpan.FromSeconds(2), CancellationToken.None)
                    .GetAwaiter().GetResult();
                Assert.IsTrue(open);
            }
            finally
            {
                listener.Stop();
            }
        }

        [TestMethod]
        public void ClosedPortReportsNotOpen()
        {
            int port;
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                port = ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
            bool open = TcpConnectScanner.IsOpenAsync(
                IPAddress.Loopback, port, TimeSpan.FromSeconds(2), CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert.IsFalse(open);
        }

        [TestMethod]
        public void CancelledScanThrowsCancellation()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.ThrowsExactly<OperationCanceledException>(() =>
                    TcpConnectScanner.IsOpenAsync(
                        IPAddress.Loopback, 443, TimeSpan.FromSeconds(2), cancellation.Token)
                    .GetAwaiter().GetResult());
            }
        }

        [TestMethod]
        public void InvalidPortIsRejected()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                TcpConnectScanner.IsOpenAsync(
                    IPAddress.Loopback, 0, TimeSpan.FromSeconds(2), CancellationToken.None)
                .GetAwaiter().GetResult());
        }
    }
}
