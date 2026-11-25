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

        [TestMethod]
        public void DialogConnectionSelectionExcludesExistingAndUnrelatedHandles()
        {
            var before = new HashSet<IntPtr> { new IntPtr(1) };
            var connections = new[]
            {
                new RasNativeMethods.RasConn { Handle = new IntPtr(1), EntryName = "VPN" },
                new RasNativeMethods.RasConn { Handle = new IntPtr(2), EntryName = "Other" },
                new RasNativeMethods.RasConn { Handle = IntPtr.Zero, EntryName = "VPN" },
                new RasNativeMethods.RasConn { Handle = new IntPtr(3), EntryName = "vpn" }
            };
            Assert.AreEqual(new IntPtr(3), RasConnectionClient.FindNewDialogConnection(connections, before, "VPN"));
        }

        [TestMethod]
        public void DialogConnectionSelectionRejectsAmbiguousNewHandles()
        {
            var connections = new[]
            {
                new RasNativeMethods.RasConn { Handle = new IntPtr(1), EntryName = "VPN" },
                new RasNativeMethods.RasConn { Handle = new IntPtr(2), EntryName = "VPN" }
            };
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                RasConnectionClient.FindNewDialogConnection(connections, new HashSet<IntPtr>(), "VPN"));
        }

        [TestMethod]
        public void DialogConnectionSelectionDoesNotAdoptExistingHandle()
        {
            var connections = new[]
            {
                new RasNativeMethods.RasConn { Handle = new IntPtr(1), EntryName = "VPN" }
            };
            Assert.AreEqual(IntPtr.Zero, RasConnectionClient.FindNewDialogConnection(connections,
                new HashSet<IntPtr> { new IntPtr(1) }, "VPN"));
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
