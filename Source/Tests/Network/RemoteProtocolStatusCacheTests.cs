using System;
using System.Runtime.InteropServices;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Common.Network;

namespace Tests.Network
{
    [TestClass]
    public class RemoteProtocolStatusCacheTests
    {
        [TestMethod]
        public void RpcFailuresAreSharedAndRetryClearsThem()
        {
            string host = Guid.NewGuid().ToString("N") + ".example";
            Assert.IsTrue(RemoteProtocolStatusCache.ReportFailure(host,
                RemoteProtocolStatusCache.Protocol.WmiDcom,
                new COMException("RPC unavailable", unchecked((int)0x800706BA))));
            RemoteProtocolStatusCache.Failure failure;
            Assert.IsTrue(RemoteProtocolStatusCache.TryGetFailure(
                @"\\" + host.ToUpperInvariant() + @"\root\cimv2",
                RemoteProtocolStatusCache.Protocol.WmiDcom, out failure));
            RemoteProtocolStatusCache.Retry(host, RemoteProtocolStatusCache.Protocol.WmiDcom);
            Assert.IsFalse(RemoteProtocolStatusCache.TryGetFailure(host,
                RemoteProtocolStatusCache.Protocol.WmiDcom, out failure));
        }

        [TestMethod]
        public void AuthorizationIsNotAProtocolFailure()
        {
            string host = Guid.NewGuid().ToString("N") + ".example";
            Assert.IsFalse(RemoteProtocolStatusCache.ReportFailure(host,
                RemoteProtocolStatusCache.Protocol.WmiDcom,
                new COMException("Access denied", unchecked((int)0x80070005))));
            RemoteProtocolStatusCache.Failure failure;
            Assert.IsFalse(RemoteProtocolStatusCache.TryGetFailure(host,
                RemoteProtocolStatusCache.Protocol.WmiDcom, out failure));
        }

        [TestMethod]
        public void DifferentProtocolsAndLocalhostAreIndependent()
        {
            string host = Guid.NewGuid().ToString("N") + ".example";
            RemoteProtocolStatusCache.ReportFailure(host,
                RemoteProtocolStatusCache.Protocol.WmiDcom, new TimeoutException());
            RemoteProtocolStatusCache.Failure failure;
            Assert.IsFalse(RemoteProtocolStatusCache.TryGetFailure(host,
                RemoteProtocolStatusCache.Protocol.WsManHttps, out failure));
            Assert.IsFalse(RemoteProtocolStatusCache.ReportFailure("localhost",
                RemoteProtocolStatusCache.Protocol.WmiDcom, new TimeoutException()));
            RemoteProtocolStatusCache.ReportSuccess(host, RemoteProtocolStatusCache.Protocol.WmiDcom);
        }
        [TestMethod]
        public void ConcurrentTransportReports_RemainVisibleAcrossConsumers()
        {
            string host = Guid.NewGuid().ToString("N") + ".example";
            Parallel.For(0, 32, index =>
            {
                Assert.IsTrue(RemoteProtocolStatusCache.ReportFailure(host,
                    RemoteProtocolStatusCache.Protocol.WmiDcom,
                    new COMException("RPC unavailable", unchecked((int)0x800706BA))));
            });

            RemoteProtocolStatusCache.Failure failure;
            Assert.IsTrue(RemoteProtocolStatusCache.TryGetFailure(host,
                RemoteProtocolStatusCache.Protocol.WmiDcom, out failure));
            Assert.IsTrue(failure.ExpiresAt > DateTimeOffset.UtcNow);

            RemoteProtocolStatusCache.ReportSuccess(host, RemoteProtocolStatusCache.Protocol.WmiDcom);
            Assert.IsFalse(RemoteProtocolStatusCache.TryGetFailure(host,
                RemoteProtocolStatusCache.Protocol.WmiDcom, out failure));
        }

        [TestMethod]
        public void NestedSocketFailuresAreTransportFailures_ButProviderErrorsAreNot()
        {
            string host = Guid.NewGuid().ToString("N") + ".example";
            var transport = new InvalidOperationException("Wrapped", new SocketException((int)SocketError.HostUnreachable));
            Assert.IsTrue(RemoteProtocolStatusCache.ReportFailure(host,
                RemoteProtocolStatusCache.Protocol.WmiDcom, transport));

            RemoteProtocolStatusCache.Retry(host, RemoteProtocolStatusCache.Protocol.WmiDcom);
            Assert.IsFalse(RemoteProtocolStatusCache.ReportFailure(host,
                RemoteProtocolStatusCache.Protocol.WmiDcom,
                new COMException("Invalid class", unchecked((int)0x80041010))));

            RemoteProtocolStatusCache.Failure failure;
            Assert.IsFalse(RemoteProtocolStatusCache.TryGetFailure(host,
                RemoteProtocolStatusCache.Protocol.WmiDcom, out failure));
        }

    }
}
