using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Network.WhoIs;

namespace Tests.Network
{
    [TestClass]
    public class WhoisQueryTests
    {
        [TestMethod]
        public void DefaultServerUsesTopLevelDomain()
        {
            Assert.AreEqual("com.whois-servers.net", WhoisQuery.GetDefaultServer("example.com"));
            Assert.AreEqual("uk.whois-servers.net", WhoisQuery.GetDefaultServer("example.co.uk"));
        }

        [TestMethod]
        public void ExactMatchPrefixIsRetainedExceptForCanada()
        {
            Assert.AreEqual("=example.com", WhoisQuery.FormatQuery("example.com"));
            Assert.AreEqual("example.CA", WhoisQuery.FormatQuery("example.CA"));
            Assert.AreEqual("=example.org", WhoisQuery.FormatQuery("=example.org"));
        }

        [TestMethod]
        public void InputWithLineBreakOrMissingSuffixIsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(() => WhoisQuery.NormalizeDomain("example.com\r\nINJECT"));
            Assert.ThrowsExactly<ArgumentException>(() => WhoisQuery.NormalizeDomain("localhost"));
            Assert.ThrowsExactly<ArgumentException>(() => WhoisQuery.NormalizeDomain("a..com"));
        }

        [TestMethod]
        public void ReferralCanBeReadFromSingleLineFeedResponse()
        {
            Assert.AreEqual("whois.example.net",
                WhoisQuery.ParseReferral("Status: active\nWhois Server: whois.example.net\n"));
        }

        [TestMethod]
        public void ReferralParsingIsCaseInsensitive()
        {
            Assert.AreEqual("whois.example.net",
                WhoisQuery.ParseReferral("WHOIS SERVER: whois.example.net\r\n"));
        }

        [TestMethod]
        public void InvalidReferralUrlOrPortIsIgnored()
        {
            Assert.IsNull(WhoisQuery.ParseReferral("Whois Server: https://example.net\n"));
            Assert.IsNull(WhoisQuery.ParseReferral("Whois Server: whois.example.net:8443\n"));
            Assert.IsNull(WhoisQuery.ParseReferral("Whois Server: 127.0.0.1\n"));
        }

        [TestMethod]
        public void MissingReferralReturnsNull()
        {
            Assert.IsNull(WhoisQuery.ParseReferral("status: active\n"));
        }

        [TestMethod]
        public void CancellationPreventsNetworkActivity()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.ThrowsExactly<OperationCanceledException>(() =>
                    WhoisQuery.LookupAsync("example.com", cancellation.Token).GetAwaiter().GetResult());
            }
        }
        [TestMethod]
        public void ResponseReaderRejectsExcessivePayload()
        {
            byte[] data = Encoding.ASCII.GetBytes("response body");
            using (var stream = new MemoryStream(data))
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    WhoisQuery.ReadResponseAsync(stream, 5, CancellationToken.None).GetAwaiter().GetResult());
        }

        [TestMethod]
        public void ResponseReaderPreservesPayloadWithinLimit()
        {
            byte[] data = Encoding.ASCII.GetBytes("ok");
            using (var stream = new MemoryStream(data))
                Assert.AreEqual("ok",
                    WhoisQuery.ReadResponseAsync(stream, 2, CancellationToken.None).GetAwaiter().GetResult());
        }

        [TestMethod]
        public void NonAsciiHostnameIsRejectedBeforeNetwork()
        {
            Assert.ThrowsExactly<ArgumentException>(() => WhoisQuery.NormalizeDomain("tést.example"));
        }

    }
}
