using System;
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
            Assert.AreEqual(IntPtr.Size == 8 ? 8L : 4L,
                Marshal.OffsetOf(typeof(RasNativeMethods.RasConn), "Handle").ToInt64());
            Assert.AreEqual(8, Marshal.SizeOf(typeof(RasNativeMethods.Luid)));
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
