using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        [TestMethod]
        public void AsyncCollector_QueriesInOriginalOrder()
        {
            var requested = new List<QueryType>();
            var response = new Mock<IDnsQueryResponse>();
            response.SetupGet(x => x.Answers).Returns(new List<DnsResourceRecord>());
            var results = DNSLookup.CollectRecordsAsync((type, token) =>
            {
                requested.Add(type);
                return Task.FromResult(response.Object);
            }, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(4, results.Count);
            CollectionAssert.AreEqual(
                new[] { QueryType.A, QueryType.MX, QueryType.NS, QueryType.SOA }, requested);
        }

        [TestMethod]
        public void AsyncCollector_PreservesErrorStatuses()
        {
            var response = new Mock<IDnsQueryResponse>();
            response.SetupGet(x => x.HasError).Returns(true);
            response.SetupGet(x => x.ErrorMessage).Returns("NotExistentDomain");
            var rows = DNSLookup.CollectRecordsAsync(
                (type, token) => Task.FromResult(response.Object), CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert.AreEqual(4, rows.Count);
            foreach (var row in rows)
                Assert.AreEqual("NotExistentDomain", row.Status);
        }

        [TestMethod]
        public void AsyncCollector_CancelledBeforeFirstQuery()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.ThrowsExactly<OperationCanceledException>(() =>
                    DNSLookup.CollectRecordsAsync((type, token) =>
                        throw new InvalidOperationException("Must not query"), cancellation.Token)
                        .GetAwaiter().GetResult());
            }
        }

        [TestMethod]
        public void AsyncCollector_CancellationStopsRemainingQueries()
        {
            int count = 0;
            using (var cancellation = new CancellationTokenSource())
            {
                var response = new Mock<IDnsQueryResponse>();
                response.SetupGet(x => x.Answers).Returns(new List<DnsResourceRecord>());
                Assert.ThrowsExactly<OperationCanceledException>(() =>
                    DNSLookup.CollectRecordsAsync((type, token) =>
                    {
                        count++;
                        cancellation.Cancel();
                        return Task.FromResult(response.Object);
                    }, cancellation.Token).GetAwaiter().GetResult());
            }
            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public void AsyncCollector_RejectsNullDelegate()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                DNSLookup.CollectRecordsAsync(null, CancellationToken.None).GetAwaiter().GetResult());
        }

    }
}
