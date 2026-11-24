using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Network;

namespace Tests.Network
{
    [TestClass]
    public class PortScanSessionTests
    {
        [TestMethod]
        public async Task DeliversAllResultsBeforeCompletion()
        {
            var results = new ConcurrentBag<ScanResult>();
            await PortScanSession.RunAsync(new[] { IPAddress.Loopback }, 1, 64,
                async (address, port, token) =>
                {
                    await Task.Yield();
                    return port % 2 == 0;
                }, result => results.Add(result), CancellationToken.None);

            Assert.AreEqual(64, results.Count);
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 64)
                .Where(port => port % 2 == 0).ToArray(),
                results.Where(result => result != null)
                    .Select(result => result.RemoteEndPoint.Port).ToArray());
        }

        [TestMethod]
        public async Task BoundsConcurrencyAndDrainsOutstandingProbes()
        {
            int active = 0;
            int started = 0;
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var saturated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var scan = PortScanSession.RunAsync(new[] { IPAddress.Loopback }, 1, 64,
                async (address, port, token) =>
                {
                    Interlocked.Increment(ref active);
                    if (Interlocked.Increment(ref started) == 32)
                        saturated.TrySetResult(true);
                    try { return await gate.Task; }
                    finally { Interlocked.Decrement(ref active); }
                }, result => { }, CancellationToken.None);

            try
            {
                Assert.AreSame(saturated.Task, await Task.WhenAny(saturated.Task, Task.Delay(5000)));
                Assert.AreEqual(32, Volatile.Read(ref started));
                Assert.AreEqual(32, Volatile.Read(ref active));
                Assert.IsFalse(scan.IsCompleted);
            }
            finally
            {
                gate.TrySetResult(true);
                await scan;
            }
            Assert.AreEqual(64, started);
            Assert.AreEqual(0, active);
        }

        [TestMethod]
        public async Task CancellationDrainsProbesAndSuppressesLateResults()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                int started = 0;
                int reported = 0;
                var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var scan = PortScanSession.RunAsync(new[] { IPAddress.Loopback }, 1, 64,
                    async (address, port, token) =>
                    {
                        Interlocked.Increment(ref started);
                        return await gate.Task;
                    }, result => Interlocked.Increment(ref reported), cancellation.Token);

                cancellation.Cancel();
                Assert.IsFalse(scan.IsCompleted);
                gate.TrySetResult(true);
                try
                {
                    await scan;
                    Assert.Fail("Expected cancellation.");
                }
                catch (OperationCanceledException) { }
                Assert.AreEqual(32, started);
                Assert.AreEqual(0, reported);

                await PortScanSession.RunAsync(new[] { IPAddress.Loopback }, 80, 80,
                    (address, port, token) => Task.FromResult(true),
                    result => Interlocked.Increment(ref reported), CancellationToken.None);
                Assert.AreEqual(1, reported);
            }
        }

        [TestMethod]
        public async Task RejectsExcessiveScopeBeforeProbing()
        {
            int started = 0;
            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
                PortScanSession.RunAsync(new[] { IPAddress.Loopback }, 1, 4097,
                    (address, port, token) =>
                    {
                        Interlocked.Increment(ref started);
                        return Task.FromResult(false);
                    }, result => { }, CancellationToken.None));
            Assert.AreEqual(0, started);
        }

        [TestMethod]
        public async Task ProbeFailureDoesNotLoseOtherResults()
        {
            var results = new ConcurrentBag<ScanResult>();
            await PortScanSession.RunAsync(new[] { IPAddress.Loopback }, 1, 3,
                (address, port, token) => port == 2
                    ? Task.FromException<bool>(new InvalidOperationException("test failure"))
                    : Task.FromResult(true),
                result => results.Add(result), CancellationToken.None);
            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(1, results.Count(result => result == null));
        }
    }
}
