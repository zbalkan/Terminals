using System;
using System.Collections.Generic;
using DnsClient;
using DnsClient.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Terminals.Network;

namespace Tests.Network
{
    [TestClass]
    public class DnsLookupTests
    {
        [TestMethod]
        public void EmptyAnswers_ReportNoRecordsWithoutIndexing()
        {
            var response = new Mock<IDnsQueryResponse>();
            response.SetupGet(x => x.Answers).Returns(new List<DnsResourceRecord>());
            var results = DNSLookup.CollectRecords(type => response.Object);
            Assert.AreEqual(4, results.Count);
            foreach (var row in results)
                Assert.AreEqual("No records", row.Status);
        }

        [TestMethod]
        public void ErrorResponse_ReportsDnsStatus()
        {
            var response = new Mock<IDnsQueryResponse>();
            response.SetupGet(x => x.HasError).Returns(true);
            response.SetupGet(x => x.ErrorMessage).Returns("NotExistentDomain");
            var rows = DNSLookup.CollectRecords(type => response.Object);
            Assert.AreEqual(4, rows.Count);
            foreach (var row in rows)
                Assert.AreEqual("NotExistentDomain", row.Status);
        }

        [TestMethod]
        public void Records_AreQueriedInOriginalOrder()
        {
            var requested = new List<QueryType>();
            var response = new Mock<IDnsQueryResponse>();
            response.SetupGet(x => x.Answers).Returns(new List<DnsResourceRecord>());
            DNSLookup.CollectRecords(type =>
            {
                requested.Add(type);
                return response.Object;
            });
            CollectionAssert.AreEqual(
                new[] { QueryType.A, QueryType.MX, QueryType.NS, QueryType.SOA },
                requested);
        }

        [TestMethod]
        public void NullQuery_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(
                () => DNSLookup.CollectRecords(null));
        }
    }
}
