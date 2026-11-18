using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Network;

namespace Tests.Network
{
    [TestClass]
    public class TraceRouteTests
    {
        [TestMethod]
        public void FirstDnsAddressIsSelected()
        {
            IPAddress first = IPAddress.Parse("2001:db8::1");
            Assert.AreEqual(first, TraceRoute.SelectDestination(new[] { first, IPAddress.Loopback }));
        }

        [TestMethod]
        public void EmptyDnsAnswerFails()
        {
            Assert.ThrowsExactly<ArgumentException>(() => TraceRoute.SelectDestination(new IPAddress[0]));
        }

        [TestMethod]
        public void MissingDnsAnswerFails()
        {
            Assert.ThrowsExactly<ArgumentException>(() => TraceRoute.SelectDestination(null));
        }

        [TestMethod]
        public void OnlyDestinationSuccessCompletesTrace()
        {
            IPAddress target = IPAddress.Parse("192.0.2.10");
            Assert.IsTrue(TraceRoute.ReachedDestination(IPStatus.Success, target, target));
            Assert.IsFalse(TraceRoute.ReachedDestination(IPStatus.Success, IPAddress.Loopback, target));
            Assert.IsFalse(TraceRoute.ReachedDestination(IPStatus.TtlExpired, target, target));
            Assert.IsFalse(TraceRoute.ReachedDestination(IPStatus.TimedOut, null, target));
        }

        [TestMethod]
        public void HopDoesNotResolveDnsInConstructor()
        {
            var hop = new TraceRouteHopData(2, IPAddress.Loopback, 12, IPStatus.TtlExpired);
            Assert.AreEqual((byte)2, hop.Count);
            Assert.AreEqual(12L, hop.RoundTripTime);
            Assert.AreEqual(string.Empty, hop.HostName);
        }

        [TestMethod]
        public void CancellationBeforeStartPreventsNetworkActivity()
        {
            using (var trace = new TraceRoute { Destination = "example.invalid" })
            using (var token = new CancellationTokenSource())
            {
                token.Cancel();
                Assert.ThrowsExactly<OperationCanceledException>(() =>
                    trace.RunAsync(token.Token, null).GetAwaiter().GetResult());
            }
        }

        [TestMethod]
        public void ExcessiveHopLimitIsRejectedBeforeNetworkActivity()
        {
            using (var trace = new TraceRoute { Destination = "example.invalid", HopLimit = 129 })
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                    trace.RunAsync(CancellationToken.None, null).GetAwaiter().GetResult());
        }
    }
}
