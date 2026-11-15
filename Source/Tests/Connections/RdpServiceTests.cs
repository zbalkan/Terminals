using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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
        public void Connect_DoesNotCallTerminalServicesDiscoveryDirectly()
        {
            // This checks the actual compiled Connect() method without activating
            // the RDP ActiveX control. Live WTS/audit behavior still requires
            // an integration test with a Windows RDP server.
            MethodInfo connect = typeof(RDPConnection).GetMethod("Connect");
            Assert.IsNotNull(connect);
            foreach (MethodBase invoked in GetDirectCalls(connect))
            {
                bool startsDiscovery = invoked.Name == "CheckForTerminalServer" ||
                    invoked.Name == "LoadTerminalServerAsync" ||
                    (invoked.DeclaringType == typeof(TerminalServer) && invoked.Name == "LoadServer");
                Assert.IsFalse(startsDiscovery, "RDPConnection.Connect() initiates Terminal Services discovery: " + invoked);
            }
        }

        private static IEnumerable<MethodBase> GetDirectCalls(MethodInfo method)
        {
            var opCodes = new Dictionary<ushort, OpCode>();
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(OpCode))
                {
                    var opcode = (OpCode)field.GetValue(null);
                    opCodes[unchecked((ushort)opcode.Value)] = opcode;
                }
            }

            byte[] il = method.GetMethodBody().GetILAsByteArray();
            var called = new List<MethodBase>();
            for (int position = 0; position < il.Length;)
            {
                ushort value = il[position++];
                if (value == 0xFE)
                    value = (ushort)(0xFE00 | il[position++]);
                OpCode opcode = opCodes[value];
                switch (opcode.OperandType)
                {
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineVar:
                        position++;
                        break;
                    case OperandType.InlineVar:
                        position += 2;
                        break;
                    case OperandType.InlineSwitch:
                        int jumpCount = BitConverter.ToInt32(il, position);
                        position += 4 + jumpCount * 4;
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        position += 8;
                        break;
                    case OperandType.InlineMethod:
                        called.Add(method.Module.ResolveMethod(BitConverter.ToInt32(il, position)));
                        position += 4;
                        break;
                    case OperandType.InlineI:
                    case OperandType.InlineBrTarget:
                    case OperandType.InlineField:
                    case OperandType.InlineSig:
                    case OperandType.InlineString:
                    case OperandType.InlineTok:
                    case OperandType.InlineType:
                    case OperandType.ShortInlineR:
                        position += 4;
                        break;
                    default:
                        Assert.Fail("Unhandled IL operand type: " + opcode.OperandType);
                        break;
                }
            }
            return called;
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
