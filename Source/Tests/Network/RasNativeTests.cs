using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Connections.RasNative;

namespace Tests.Network
{
    [TestClass]
    public class RasNativeTests
    {
        [TestMethod]
        public void DialParametersUseUnicodeInlineCharacterBuffers()
        {
            Assert.AreEqual(4L, Marshal.OffsetOf(typeof(RasNativeMethods.RasDialParams), "EntryName").ToInt64());
            Assert.AreEqual(518L, Marshal.OffsetOf(typeof(RasNativeMethods.RasDialParams), "PhoneNumber").ToInt64());
            Assert.AreEqual(4L, Marshal.OffsetOf(typeof(RasNativeMethods.RasEntryName), "EntryName").ToInt64());
        }

        [TestMethod]
        public void ConnectionHandleIsPointerSizedAndLuidIsEightBytes()
        {
            Assert.AreEqual(4L,
                Marshal.OffsetOf(typeof(RasNativeMethods.RasConn), "Handle").ToInt64());
            Assert.AreEqual(8, Marshal.SizeOf(typeof(RasNativeMethods.Luid)));
        }

        // Windows SDK Ras.h and RasDlg.h use pshpack4.h (four-byte packing).
        // These are native Unicode layout expectations, not values derived from Marshal.
        [TestMethod]
        [TestCategory("RasAbi")]
        public void NativeStructuresMatchWindowsSdkLayout()
        {
            bool x64 = IntPtr.Size == 8;
            AssertLayout(typeof(RasNativeMethods.RasDialParams), x64 ? 2120 : 2112,
                "EntryName", 4, "PhoneNumber", 518, "CallbackNumber", 776,
                "UserName", 1034, "Password", 1548, "Domain", 2062,
                "SubEntry", 2096, "CallbackId", 2100,
                "IfIndex", x64 ? 2108 : 2104, "EncryptedPassword", x64 ? 2112 : 2108);
            AssertLayout(typeof(RasNativeMethods.RasDialDialog), x64 ? 48 : 36,
                "Owner", 4, "Flags", x64 ? 12 : 8, "X", x64 ? 16 : 12,
                "Y", x64 ? 20 : 16, "SubEntry", x64 ? 24 : 20,
                "Error", x64 ? 28 : 24, "Reserved", x64 ? 32 : 28,
                "Reserved2", x64 ? 40 : 32);
            AssertLayout(typeof(RasNativeMethods.RasConn), x64 ? 1388 : 1384,
                "Handle", 4, "EntryName", x64 ? 12 : 8, "DeviceType", x64 ? 526 : 522,
                "DeviceName", x64 ? 560 : 556, "PhonebookPath", x64 ? 818 : 814,
                "SubEntry", x64 ? 1340 : 1336, "EntryGuid", x64 ? 1344 : 1340,
                "Flags", x64 ? 1360 : 1356, "Luid", x64 ? 1364 : 1360,
                "CorrelationId", x64 ? 1372 : 1368);
            AssertLayout(typeof(RasNativeMethods.RasConnStatus), 608,
                "State", 4, "Error", 8, "DeviceType", 12, "DeviceName", 46,
                "PhoneNumber", 304, "LocalEndpoint", 564, "RemoteEndpoint", 584, "SubState", 604);
            AssertLayout(typeof(RasNativeMethods.RasTunnelEndpoint), 20,
                "Type", 0, "Address0", 4, "Address1", 8, "Address2", 12, "Address3", 16);
            AssertLayout(typeof(RasNativeMethods.RasEntryName), 1048,
                "EntryName", 4, "Flags", 520, "PhonebookPath", 524);
            AssertLayout(typeof(RasNativeMethods.RasPppIp), 80,
                "Error", 4, "IpAddress", 8, "ServerIpAddress", 40, "Options", 72, "ServerOptions", 76);
            AssertLayout(typeof(RasNativeMethods.Luid), 8, "LowPart", 0, "HighPart", 4);
        }

        [TestMethod]
        [TestCategory("RasAbi")]
        public void ArchitectureMatchesRequestedTestHost()
        {
            string expected = Environment.GetEnvironmentVariable("TERMINALS_RAS_ABI_PLATFORM");
            if (!string.IsNullOrEmpty(expected))
                Assert.AreEqual(expected, Environment.Is64BitProcess ? "x64" : "x86");
        }

        private static void AssertLayout(Type type, int size, params object[] fields)
        {
            Assert.AreEqual(size, Marshal.SizeOf(type), type.Name + " size");
            for (int i = 0; i < fields.Length; i += 2)
            {
                string field = (string)fields[i];
                Assert.AreEqual((long)(int)fields[i + 1], Marshal.OffsetOf(type, field).ToInt64(),
                    type.Name + "." + field);
            }
        }

        [TestMethod]
        public void NativePhonebookEnumerationAcceptsInitializedBuffer()
        {
            var entries = RasNativeMethods.EnumerateEntries();
            Assert.IsNotNull(entries);
            foreach (var entry in entries)
                Assert.IsFalse(string.IsNullOrEmpty(entry.EntryName));
        }

        [TestMethod]
        public void NativeConnectionEnumerationAcceptsInitializedBuffer()
        {
            var connections = RasNativeMethods.EnumerateConnections();
            Assert.IsNotNull(connections);
            foreach (var connection in connections)
                Assert.AreNotEqual(IntPtr.Zero, connection.Handle);
        }

        private sealed class FakeRas
        {
            internal uint DialError;
            internal IntPtr Candidate = new IntPtr(42);
            internal uint HangUpError;
            internal int Dials;
            internal int HangUps;
            internal int StatusCalls;
            internal int Waits;
            internal int WaitLimit;
            internal readonly Queue<uint> StatusResults = new Queue<uint>();

            internal RasConnectionClient CreateClient()
            {
                return new RasConnectionClient(Dial, HangUp, Status, elapsed => Waits++ < WaitLimit);
            }

            private uint Dial(ref RasNativeMethods.RasDialParams parameters, ref IntPtr connection)
            {
                Dials++;
                Assert.AreEqual(IntPtr.Zero, connection);
                connection = Candidate;
                return DialError;
            }

            private uint HangUp(IntPtr connection)
            {
                Assert.AreEqual(Candidate, connection);
                HangUps++;
                return HangUpError;
            }

            private uint Status(IntPtr connection, ref RasNativeMethods.RasConnStatus status)
            {
                Assert.AreEqual(Candidate, connection);
                StatusCalls++;
                status.State = 0x2000;
                return StatusResults.Count == 0 ? 0 : StatusResults.Dequeue();
            }
        }

        [TestMethod]
        public void DialogCannotAcquireAnUnprovenReference()
        {
            var native = new FakeRas();
            using (var ras = native.CreateClient())
            {
                Assert.ThrowsExactly<NotSupportedException>(() => ras.DialDialog(IntPtr.Zero));
                Assert.IsFalse(ras.HasOutstandingHandle);
                Assert.AreEqual(0, native.Dials);
                Assert.AreEqual(0, native.HangUps);
            }
        }

        [TestMethod]
        public void HangUpWaitsForInvalidHandleAndAllowsRedial()
        {
            var native = new FakeRas { WaitLimit = 1 };
            native.StatusResults.Enqueue(0);
            native.StatusResults.Enqueue(6);
            using (var ras = native.CreateClient())
            {
                Assert.AreEqual(0U, ras.Dial("u", "p", "d"));
                Assert.AreEqual(0U, ras.HangUp());
                Assert.AreEqual(2, native.StatusCalls);
                Assert.IsFalse(ras.HasOutstandingHandle);
                Assert.AreEqual(0U, ras.HangUp());
                Assert.AreEqual(1, native.HangUps);
                Assert.AreEqual(0U, ras.Dial("u", "p", "d"));
                native.StatusResults.Enqueue(6);
            }
            Assert.AreEqual(2, native.HangUps);
        }

        [TestMethod]
        public void HangUpFailureRetainsHandleAndAllowsCleanupRetry()
        {
            var native = new FakeRas { HangUpError = 5 };
            using (var ras = native.CreateClient())
            {
                Assert.AreEqual(0U, ras.Dial("u", "p", "d"));
                Assert.AreEqual(5U, ras.HangUp());
                Assert.IsTrue(ras.HasOutstandingHandle);
                Assert.ThrowsExactly<InvalidOperationException>(() => ras.Dial("u", "p", "d"));
                Assert.AreEqual(0, native.StatusCalls);
                native.HangUpError = 0;
                native.StatusResults.Enqueue(6);
                Assert.AreEqual(0U, ras.HangUp());
            }
            Assert.AreEqual(2, native.HangUps);
        }

        [TestMethod]
        public void AcceptedHangUpTimeoutDoesNotReleaseSharedReferenceTwice()
        {
            var native = new FakeRas();
            using (var ras = native.CreateClient())
            {
                ras.Dial("u", "p", "d");
                Assert.AreEqual(RasConnectionClient.ErrorTimeout, ras.HangUp());
                Assert.IsTrue(ras.HasOutstandingHandle);
                Assert.IsFalse(ras.Connected);
                Assert.ThrowsExactly<InvalidOperationException>(() => ras.Dial("u", "p", "d"));
                Assert.AreEqual(RasConnectionClient.ErrorTimeout, ras.HangUp());
                native.StatusResults.Enqueue(6);
                Assert.AreEqual(0U, ras.HangUp());
            }
            Assert.AreEqual(1, native.HangUps);
        }

        [TestMethod]
        public void StatusErrorAfterAcceptedHangUpRetainsPendingToken()
        {
            var native = new FakeRas();
            native.StatusResults.Enqueue(5);
            using (var ras = native.CreateClient())
            {
                ras.Dial("u", "p", "d");
                Assert.AreEqual(5U, ras.HangUp());
                Assert.IsTrue(ras.HasOutstandingHandle);
                native.StatusResults.Enqueue(6);
                Assert.AreEqual(0U, ras.HangUp());
            }
            Assert.AreEqual(1, native.HangUps);
        }

        [TestMethod]
        public void FailedDialWithHandlePerformsVerifiedCleanup()
        {
            var native = new FakeRas { DialError = 691 };
            native.StatusResults.Enqueue(6);
            using (var ras = native.CreateClient())
            {
                Assert.AreEqual(691U, ras.Dial("u", "p", "d"));
                Assert.IsFalse(ras.HasOutstandingHandle);
                Assert.AreEqual(1, native.HangUps);
            }
        }

        [TestMethod]
        public void FailedDialWithIncompleteCleanupRetainsReference()
        {
            var native = new FakeRas { DialError = 691, HangUpError = 5 };
            using (var ras = native.CreateClient())
            {
                var error = Assert.ThrowsExactly<System.ComponentModel.Win32Exception>(() => ras.Dial("u", "p", "d"));
                Assert.AreEqual(5, error.NativeErrorCode);
                StringAssert.Contains(error.Message, "691");
                Assert.IsTrue(ras.HasOutstandingHandle);
                Assert.ThrowsExactly<InvalidOperationException>(() => ras.Dial("u", "p", "d"));
                native.HangUpError = 0;
                native.StatusResults.Enqueue(6);
            }
            Assert.AreEqual(2, native.HangUps);
        }

        [TestMethod]
        public void FailedDialWithoutHandleDoesNotHangUp()
        {
            var native = new FakeRas { Candidate = IntPtr.Zero, DialError = 691 };
            using (var ras = native.CreateClient())
            {
                Assert.AreEqual(691U, ras.Dial("u", "p", "d"));
                Assert.IsFalse(ras.HasOutstandingHandle);
            }
            Assert.AreEqual(0, native.HangUps);
        }

        [TestMethod]
        public void SuccessfulDialWithoutHandleIsRejected()
        {
            var native = new FakeRas { Candidate = IntPtr.Zero };
            using (var ras = native.CreateClient())
                Assert.ThrowsExactly<InvalidOperationException>(() => ras.Dial("u", "p", "d"));
            Assert.AreEqual(0, native.HangUps);
        }

        [TestMethod]
        public void DisposalFailureIsRetryableButDialRemainsBlocked()
        {
            var native = new FakeRas();
            var ras = native.CreateClient();
            ras.Dial("u", "p", "d");
            Assert.ThrowsExactly<System.ComponentModel.Win32Exception>(() => ras.Dispose());
            Assert.IsTrue(ras.HasOutstandingHandle);
            Assert.ThrowsExactly<ObjectDisposedException>(() => ras.Dial("u", "p", "d"));
            native.StatusResults.Enqueue(6);
            ras.Dispose();
            ras.Dispose();
            Assert.IsFalse(ras.HasOutstandingHandle);
            Assert.AreEqual(1, native.HangUps);
        }

        [TestMethod]
        public void AlreadyInvalidHandleIsReleasedWithoutPolling()
        {
            var native = new FakeRas { HangUpError = 6 };
            using (var ras = native.CreateClient())
            {
                ras.Dial("u", "p", "d");
                Assert.AreEqual(0U, ras.HangUp());
                Assert.IsFalse(ras.HasOutstandingHandle);
            }
            Assert.AreEqual(0, native.StatusCalls);
            Assert.AreEqual(1, native.HangUps);
        }

        [TestMethod]
        public void StatusQueryFailureDoesNotDiscardConnection()
        {
            var native = new FakeRas();
            native.StatusResults.Enqueue(5);
            using (var ras = native.CreateClient())
            {
                ras.Dial("u", "p", "d");
                Assert.ThrowsExactly<System.ComponentModel.Win32Exception>(() => { var connected = ras.Connected; });
                Assert.IsTrue(ras.HasOutstandingHandle);
                native.StatusResults.Enqueue(6);
            }
        }

        [TestMethod]
        public void ActiveReferenceRejectsRepeatedDialAndDialog()
        {
            var native = new FakeRas();
            using (var ras = native.CreateClient())
            {
                ras.Dial("u", "p", "d");
                Assert.ThrowsExactly<InvalidOperationException>(() => ras.Dial("u", "p", "d"));
                Assert.ThrowsExactly<InvalidOperationException>(() => ras.DialDialog(IntPtr.Zero));
                Assert.AreEqual(1, native.Dials);
                native.StatusResults.Enqueue(6);
            }
        }

        [TestMethod]
        public void ExternallyInvalidatedHandleAllowsRedial()
        {
            var native = new FakeRas();
            using (var ras = native.CreateClient())
            {
                ras.Dial("u", "p", "d");
                native.StatusResults.Enqueue(6);
                Assert.IsFalse(ras.Connected);
                Assert.IsFalse(ras.HasOutstandingHandle);
                Assert.AreEqual(0U, ras.Dial("u", "p", "d"));
                native.StatusResults.Enqueue(6);
            }
            Assert.AreEqual(2, native.Dials);
            Assert.AreEqual(1, native.HangUps);
        }

        [TestMethod]
        public void ConcurrentHangUpAndDisposeReleaseReferenceOnlyOnce()
        {
            var native = new FakeRas();
            var ras = native.CreateClient();
            ras.Dial("u", "p", "d");
            native.StatusResults.Enqueue(6);
            System.Threading.Tasks.Parallel.Invoke(
                () => Assert.AreEqual(0U, ras.HangUp()),
                () => ras.Dispose());
            Assert.AreEqual(1, native.HangUps);
            Assert.IsFalse(ras.HasOutstandingHandle);
        }

        [TestMethod]
        public void DisposedClientRejectsDialAndDialogBeforeCallingNativeApi()
        {
            var ras = new RasConnectionClient();
            ras.Dispose();
            ras.Dispose();
            Assert.ThrowsExactly<ObjectDisposedException>(() => ras.Dial("user", "password", "domain"));
            Assert.ThrowsExactly<ObjectDisposedException>(() => ras.DialDialog(IntPtr.Zero));
        }

        [TestMethod]
        public void DisconnectWithoutOwnedHandleIsIdempotent()
        {
            using (var ras = new RasConnectionClient())
            {
                Assert.IsFalse(ras.Connected);
                Assert.AreEqual(0U, ras.HangUp());
                Assert.AreEqual(0U, ras.HangUp());
            }
        }
    }
}
