using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Network;

namespace Tests.Network
{
    [TestClass]
    public class PingSampleHistoryTests
    {
        private static PingReplyData Sample(long count)
        {
            return new PingReplyData(count, "Reply", "host", "127.0.0.1", 32, 50, count);
        }

        [TestMethod]
        public void BoundedHistoryDiscardsOldestSamples()
        {
            var history = new PingSampleHistory(3);
            for (long i = 1; i <= 5; i++)
                history.Add(Sample(i));
            Assert.AreEqual(3, history.Rows.Count);
            Assert.AreEqual(3L, history.Rows[0].Count);
            Assert.AreEqual(5L, history.Rows[2].Count);
        }

        [TestMethod]
        public void HistoryClearAllowsFreshSession()
        {
            var history = new PingSampleHistory(2);
            history.Add(Sample(1));
            history.Clear();
            history.Add(Sample(10));
            Assert.AreEqual(1, history.Rows.Count);
            Assert.AreEqual(10L, history.Rows[0].Count);
        }

        [TestMethod]
        public void InvalidCapacityIsRejected()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PingSampleHistory(0));
        }

        [TestMethod]
        public void NullSampleIsRejected()
        {
            var history = new PingSampleHistory(5);
            Assert.ThrowsExactly<ArgumentNullException>(() => history.Add(null));
        }
    }
}
