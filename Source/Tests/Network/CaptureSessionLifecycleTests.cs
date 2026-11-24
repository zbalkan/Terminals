using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals;

namespace Tests.Network
{
    [TestClass]
    public class CaptureSessionLifecycleTests
    {
        [TestMethod]
        public void StopDuringStartupDefersCleanup()
        {
            int scheduled = 0;
            var lifecycle = new CaptureSessionLifecycle(() => scheduled++);
            lifecycle.RequestStop();
            Assert.IsTrue(lifecycle.StopRequested);
            Assert.AreEqual(0, scheduled);
            lifecycle.StartupFinished();
            Assert.AreEqual(1, scheduled);
        }

        [TestMethod]
        public void CompletedStartupDoesNotStopActiveCapture()
        {
            int scheduled = 0;
            var lifecycle = new CaptureSessionLifecycle(() => scheduled++);
            lifecycle.StartupFinished();
            Assert.IsFalse(lifecycle.StopRequested);
            Assert.AreEqual(0, scheduled);
            lifecycle.RequestStop();
            Assert.AreEqual(1, scheduled);
        }

        [TestMethod]
        public void ConcurrentStopAndStartupCompletionScheduleOnce()
        {
            for (int i = 0; i < 100; i++)
            {
                int scheduled = 0;
                var lifecycle = new CaptureSessionLifecycle(() => Interlocked.Increment(ref scheduled));
                Parallel.Invoke(lifecycle.RequestStop, lifecycle.StartupFinished,
                    lifecycle.RequestStop, lifecycle.StartupFinished);
                Assert.AreEqual(1, scheduled);
            }
        }

        [TestMethod]
        public void NewSessionDoesNotInheritCancellation()
        {
            int scheduled = 0;
            var previous = new CaptureSessionLifecycle(() => scheduled++);
            previous.RequestStop();
            previous.StartupFinished();
            var next = new CaptureSessionLifecycle(() => scheduled++);
            next.StartupFinished();
            Assert.IsFalse(next.StopRequested);
            Assert.AreEqual(1, scheduled);
            next.RequestStop();
            Assert.AreEqual(2, scheduled);
        }

        [TestMethod]
        public void CleanupCanInspectLifecycleWithoutHoldingStateLock()
        {
            CaptureSessionLifecycle lifecycle = null;
            lifecycle = new CaptureSessionLifecycle(() =>
            {
                Assert.IsTrue(lifecycle.StopRequested);
                lifecycle.RequestStop();
            });
            lifecycle.StartupFinished();
            lifecycle.RequestStop();
        }
    }
}
