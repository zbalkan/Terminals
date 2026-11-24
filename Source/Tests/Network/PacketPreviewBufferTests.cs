using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals;

namespace Tests.Network
{
    [TestClass]
    public class PacketPreviewBufferTests
    {
        [TestMethod]
        public void RetainsOnlyNewestPackets()
        {
            var buffer = new PacketPreviewBuffer<int>(3);
            for (int i = 1; i <= 5; ++i) buffer.Enqueue(i);
            CollectionAssert.AreEqual(new[] { 3, 4, 5 }, buffer.Drain());
        }

        [TestMethod]
        public void DrainClearsPendingPackets()
        {
            var buffer = new PacketPreviewBuffer<int>(2);
            buffer.Enqueue(1);
            CollectionAssert.AreEqual(new[] { 1 }, buffer.Drain());
            Assert.AreEqual(0, buffer.Drain().Count);
        }

        [TestMethod]
        public void ClearDropsPriorSessionPackets()
        {
            var buffer = new PacketPreviewBuffer<int>(2);
            buffer.Enqueue(1);
            buffer.Clear();
            buffer.Enqueue(2);
            CollectionAssert.AreEqual(new[] { 2 }, buffer.Drain());
        }

        [TestMethod]
        public void ConcurrentProducersKeepBufferBounded()
        {
            var buffer = new PacketPreviewBuffer<int>(20);
            System.Threading.Tasks.Parallel.For(0, 1000, value => buffer.Enqueue(value));
            var items = buffer.Drain();
            Assert.AreEqual(20, items.Count);
            Assert.AreEqual(20, new System.Collections.Generic.HashSet<int>(items).Count);
        }

        [TestMethod]
        public void RejectsNonPositiveCapacity()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PacketPreviewBuffer<int>(0));
        }
    }
}
