using System;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Network;

namespace Tests.Network
{
    [TestClass]
    public class PingAddressSelectionTests
    {
        [TestMethod]
        public void FirstDnsAddressIsPreserved()
        {
            IPAddress first = IPAddress.Parse("2001:db8::1");
            IPAddress second = IPAddress.Parse("192.0.2.10");
            Assert.AreEqual(first, Ping.SelectPingAddress(new[] { first, second }));
        }

        [TestMethod]
        public void EmptyDnsResponseIsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(() => Ping.SelectPingAddress(new IPAddress[0]));
        }

        [TestMethod]
        public void MissingDnsResponseIsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(() => Ping.SelectPingAddress(null));
        }
    }
}
