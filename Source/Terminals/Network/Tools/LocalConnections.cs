using System;
using System.Collections.Generic;
using System.Data;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Terminals.Network
{
    internal partial class LocalConnections : UserControl
    {
        private readonly DataTable connections = CreateTable();
        private bool refreshing;
        private bool loaded;
        private int generation;

        public LocalConnections()
        {
            InitializeComponent();
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dataGridView1.DataSource = connections;
            Disposed += (sender, args) => StopRefresh();
        }

        internal static DataTable CreateTable()
        {
            var table = new DataTable("TCP connections");
            table.Columns.Add("LocalEndpoint", typeof(string));
            table.Columns.Add("RemoteEndpoint", typeof(string));
            table.Columns.Add("State", typeof(string));
            table.PrimaryKey = new[] { table.Columns["LocalEndpoint"], table.Columns["RemoteEndpoint"] };
            return table;
        }

        private void LocalConnections_Load(object sender, EventArgs e)
        {
            loaded = true;
            StartRefresh();
        }

        private void StartRefresh()
        {
            if (!loaded || IsDisposed || Disposing || !IsHandleCreated) return;
            timer1.Start();
            RefreshConnections();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Load does not necessarily fire again after WinForms recreates a handle.
            StartRefresh();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            RefreshConnections();
        }

        private async void RefreshConnections()
        {
            if (refreshing || IsDisposed || Disposing) return;
            refreshing = true;
            int version = generation;
            try
            {
                TcpConnectionInformation[] snapshot = await Task.Run(() =>
                    IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections());
                if (IsDisposed || Disposing || version != generation) return;
                ApplySnapshot(connections, snapshot);
            }
            catch (Exception exception)
            {
                if (!IsDisposed && !Disposing && version == generation)
                    Logging.Info("Unable to enumerate local TCP connections.", exception);
            }
            finally
            {
                refreshing = false;
            }
        }

        internal static void ApplySnapshot(DataTable connections, TcpConnectionInformation[] snapshot)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (TcpConnectionInformation connection in snapshot)
            {
                string local = connection.LocalEndPoint.ToString();
                string remote = connection.RemoteEndPoint.ToString();
                string key = local + "\0" + remote;
                seen.Add(key);
                DataRow row = connections.Rows.Find(new object[] { local, remote });
                if (row == null)
                    connections.Rows.Add(local, remote, connection.State.ToString());
                else if (!string.Equals((string)row["State"], connection.State.ToString(), StringComparison.Ordinal))
                    row["State"] = connection.State.ToString();
            }

            for (int i = connections.Rows.Count - 1; i >= 0; --i)
            {
                DataRow row = connections.Rows[i];
                if (!seen.Contains((string)row["LocalEndpoint"] + "\0" + (string)row["RemoteEndpoint"]))
                    connections.Rows.RemoveAt(i);
            }
        }

        private void StopRefresh()
        {
            ++generation;
            timer1.Stop();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            StopRefresh();
            base.OnHandleDestroyed(e);
        }
    }
}
