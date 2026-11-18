using System;
using System.Data;
using System.Net;
using System.Net.NetworkInformation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Network;

namespace Tests.Network
{
    [TestClass]
    public class LocalNetworkSnapshotTests
    {
        [TestMethod]
        public void WmiArrayFormattingHandlesPrimitiveArrays()
        {
            Assert.AreEqual("10, 20", InterfacesList.FormatArray(new[] { 10, 20 }));
            Assert.AreEqual("one, two", InterfacesList.FormatArray(new[] { "one", "two" }));
        }

        [TestMethod]
        public void LocalConnectionsRetainRowsAndUpdateState()
        {
            DataTable table = LocalConnections.CreateTable();
            var first = new Connection(5000, 443, TcpState.Established);
            LocalConnections.ApplySnapshot(table, new TcpConnectionInformation[] { first });
            Assert.AreEqual(1, table.Rows.Count);
            DataRow original = table.Rows[0];

            LocalConnections.ApplySnapshot(table, new TcpConnectionInformation[]
            {
                new Connection(5000, 443, TcpState.CloseWait)
            });
            Assert.AreSame(original, table.Rows[0]);
            Assert.AreEqual("CloseWait", table.Rows[0]["State"]);
        }

        [TestMethod]
        public void LocalConnectionsRemoveStaleRows()
        {
            DataTable table = LocalConnections.CreateTable();
            LocalConnections.ApplySnapshot(table, new TcpConnectionInformation[]
            {
                new Connection(5000, 443, TcpState.Established),
                new Connection(5001, 443, TcpState.Established)
            });
            LocalConnections.ApplySnapshot(table, new TcpConnectionInformation[]
            {
                new Connection(5001, 443, TcpState.Established)
            });
            Assert.AreEqual(1, table.Rows.Count);
            Assert.AreEqual("127.0.0.1:5001", table.Rows[0]["LocalEndpoint"]);
        }

        private sealed class Connection : TcpConnectionInformation
        {
            private readonly IPEndPoint local;
            private readonly IPEndPoint remote;
            private readonly TcpState state;

            public Connection(int localPort, int remotePort, TcpState connectionState)
            {
                local = new IPEndPoint(IPAddress.Loopback, localPort);
                remote = new IPEndPoint(IPAddress.Loopback, remotePort);
                state = connectionState;
            }

            public override IPEndPoint LocalEndPoint { get { return local; } }
            public override IPEndPoint RemoteEndPoint { get { return remote; } }
            public override TcpState State { get { return state; } }
        }
    }
}
