using System;
using System.Net;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Network.NTP;

namespace Tests.Network
{
    [TestClass]
    public class NtpQueryTests
    {
        private static byte[] Request()
        {
            var packet = new byte[48];
            packet[0] = 0x23;
            // Valid nonzero NTP timestamp, deterministic and independent of the clock.
            packet[40] = 0xEC;
            packet[41] = 0x10;
            packet[42] = 0x00;
            packet[43] = 0x01;
            return packet;
        }

        private static byte[] Reply(byte[] request)
        {
            var reply = new byte[48];
            reply[0] = 0x24; // NTPv4 server
            reply[1] = 2; // stratum 2
            Array.Copy(request, 40, reply, 24, 8);
            Array.Copy(request, 40, reply, 32, 8);
            Array.Copy(request, 40, reply, 40, 8);
            return reply;
        }

        [TestMethod]
        public void EmptyHostUsesConfiguredDefault()
        {
            Assert.AreEqual(Unified.Network.SNTP.NTPClient.DefaultTimeServer, NtpQuery.NormalizeHost(" "));
        }

        [TestMethod]
        public void HostInputIsTrimmedAndUnsafeCharactersAreRejected()
        {
            Assert.AreEqual("ntp.example", NtpQuery.NormalizeHost(" ntp.example "));
            Assert.ThrowsExactly<ArgumentException>(() => NtpQuery.NormalizeHost("ntp.example/path"));
        }

        [TestMethod]
        public void TruncatedResponseIsRejected()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                NtpQuery.ParseResponse(Request(), new byte[47], DateTime.UtcNow));
        }

        [TestMethod]
        public void MismatchedOriginateTimestampIsRejected()
        {
            byte[] request = Request();
            byte[] response = Reply(request);
            response[24] ^= 1;
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                NtpQuery.ParseResponse(request, response, DateTime.UtcNow));
        }

        [TestMethod]
        public void UnsynchronizedOrWrongModeResponseIsRejected()
        {
            byte[] request = Request();
            byte[] response = Reply(request);
            response[0] = 0x27;
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                NtpQuery.ParseResponse(request, response, DateTime.UtcNow));
            response[0] = 0xE4;
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                NtpQuery.ParseResponse(request, response, DateTime.UtcNow));
        }

        [TestMethod]
        public void ValidResponseIsParsed()
        {
            byte[] request = Request();
            NtpResult result = NtpQuery.ParseResponse(request, Reply(request), DateTime.UtcNow);
            Assert.AreEqual(4, result.Version);
            Assert.AreEqual(2, result.Stratum);
            Assert.IsTrue(result.ServerTimeUtc.Year >= 2024);
        }

        [TestMethod]
        public void CancelledQueryDoesNotResolveOrSend()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.ThrowsExactly<OperationCanceledException>(() =>
                    NtpQuery.GetAsync("example.invalid", cancellation.Token).GetAwaiter().GetResult());
            }
        }
    }
}
