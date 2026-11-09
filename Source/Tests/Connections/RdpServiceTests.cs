using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Connections;
using Terminals.TerminalServices;

namespace Tests.Connections
{
    [TestClass]
    public class RdpServiceTests
    {
        private static TerminalServer SuccessfulServer(string name)
        {
            return new TerminalServer
            {
                ServerName = name,
                IsATerminalServer = true,
                Sessions = new List<Session>()
            };
        }

        [TestMethod]
        public void Construction_DoesNotQuerySessions()
        {
            int calls = 0;
            var service = new RdpService(name =>
            {
                Interlocked.Increment(ref calls);
                return SuccessfulServer(name);
            });

            Assert.IsNull(service.Server);
            Assert.IsNull(service.LoadTask);
            Assert.AreEqual(0, calls);
        }

        [TestMethod]
        public void LoadTerminalServerAsync_OnlyQueriesOncePerConnection()
        {
            int calls = 0;
            var service = new RdpService(name =>
            {
                Interlocked.Increment(ref calls);
                return SuccessfulServer(name);
            });

            var first = service.LoadTerminalServerAsync("server", false);
            var second = service.LoadTerminalServerAsync("server", false);

            Assert.AreSame(first, second);
            Assert.IsTrue(first.Wait(5000));
            Assert.AreEqual(1, calls);
            Assert.IsTrue(service.IsTerminalServer);
            Assert.AreSame(first, service.LoadTerminalServerAsync("server", false));
        }

        [TestMethod]
        public void SuccessfulQuery_RefreshReplacesCachedSessions()
        {
            int calls = 0;
            var service = new RdpService(name =>
            {
                var server = SuccessfulServer(name);
                server.ServerName = name + "-" + Interlocked.Increment(ref calls);
                return server;
            });

            var first = service.LoadTerminalServerAsync("server", false);
            Assert.IsTrue(first.Wait(5000));
            Assert.AreEqual(1, calls);
            Assert.AreEqual("server-1", service.Server.ServerName);

            var refreshed = service.LoadTerminalServerAsync("server", true);
            Assert.AreNotSame(first, refreshed);
            Assert.IsTrue(refreshed.Wait(5000));
            Assert.AreEqual(2, calls);
            Assert.AreEqual("server-2", service.Server.ServerName);
            Assert.AreSame(refreshed, service.LoadTerminalServerAsync("server", false));
        }

        [TestMethod]
        public void FailedQuery_IsCachedUntilExplicitRetry()
        {
            int calls = 0;
            var service = new RdpService(name =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new InvalidOperationException("Unavailable");
                return SuccessfulServer(name);
            });

            var first = service.LoadTerminalServerAsync("server", false);
            Assert.IsTrue(first.Wait(5000));
            Assert.IsNull(service.Server);
            Assert.IsFalse(service.IsTerminalServer);
            Assert.AreSame(first, service.LoadTerminalServerAsync("server", false));
            Assert.AreEqual(1, calls);

            var retried = service.LoadTerminalServerAsync("server", true);
            Assert.AreNotSame(first, retried);
            Assert.IsTrue(retried.Wait(5000));
            Assert.AreEqual(2, calls);
            Assert.IsTrue(service.IsTerminalServer);
        }

        [TestMethod]
        public void PartialEnumeration_IsTreatedAsFailure()
        {
            var service = new RdpService(name =>
            {
                var server = SuccessfulServer(name);
                server.Errors.Add("Enumeration failed");
                return server;
            });

            Assert.IsTrue(service.LoadTerminalServerAsync("server", false).Wait(5000));
            Assert.IsNull(service.Server);
            Assert.IsFalse(service.IsTerminalServer);
        }

        [TestMethod]
        public void RetryDuringLoading_DoesNotStartAnotherQuery()
        {
            int calls = 0;
            using (var gate = new ManualResetEventSlim(false))
            {
                var service = new RdpService(name =>
                {
                    Interlocked.Increment(ref calls);
                    gate.Wait(5000);
                    return SuccessfulServer(name);
                });
                var first = service.LoadTerminalServerAsync("server", false);
                Assert.AreSame(first, service.LoadTerminalServerAsync("server", true));
                gate.Set();
                Assert.IsTrue(first.Wait(5000));
                Assert.AreEqual(1, calls);
            }
        }
    }
}
