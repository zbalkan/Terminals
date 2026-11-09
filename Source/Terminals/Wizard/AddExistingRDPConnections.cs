using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Windows.Forms;
using Terminals.Network;
using Terminals.Connections;
using Terminals.Data;
using Terminals.Integration.Import;

namespace Terminals.Wizard
{
    internal partial class AddExistingRDPConnections : UserControl
    {
        private CancellationTokenSource discoveryCancellation;
        private int pendingRequests;
        private bool discoveryFinished;

        private ConnectionManager connectionManager;

        public delegate void DiscoveryCompleted();
        public event DiscoveryCompleted OnDiscoveryCompleted;

        public List<FavoriteConfigurationElement> DiscoveredConnections { get; private set; }

        public AddExistingRDPConnections()
        {
            this.DiscoveredConnections = new List<FavoriteConfigurationElement>();

            InitializeComponent();

            this.dataGridView1.Visible = false;
        }

        public void StartImport(ConnectionManager connectionManager)
        {
            this.connectionManager = connectionManager;
            ImportFromRegistry();
            ScanInterfaceList();
        }

        private void ImportFromRegistry()
        {
            List<FavoriteConfigurationElement> favoritesFromRegistry = ImportRdpRegistry.Import(this.connectionManager);
            lock (this.DiscoveredConnections)
            {
                this.DiscoveredConnections.AddRange(favoritesFromRegistry);
            }
        }

        /// <summary>
        /// Discover services on the local IPv4 /24 using bounded TCP connect probes.
        /// Unlike the legacy SYN scanner, this does not require raw packet privileges.
        /// </summary>
        private async void ScanInterfaceList()
        {
            discoveryCancellation = new CancellationTokenSource();
            discoveryFinished = false;
            CancellationToken token = discoveryCancellation.Token;
            try
            {
                IPAddress local = NetworkInterfaceInventory.GetFirstActiveIPv4();
                if (local == null)
                    return;

                ushort[] ports = connectionManager.SupportedPorts();
                if (ports == null || ports.Length == 0)
                    return;

                byte[] bytes = local.GetAddressBytes();
                pendingRequests = 254 * ports.Length;
                UpdateConnections();

                var tasks = new List<Task<IPEndPoint>>();
                using (var throttle = new SemaphoreSlim(32))
                {
                    try
                    {
                        for (int host = 1; host <= 254; host++)
                        {
                            var address = new IPAddress(new[] { bytes[0], bytes[1], bytes[2], (byte)host });
                            foreach (ushort port in ports)
                            {
                                token.ThrowIfCancellationRequested();
                                await throttle.WaitAsync(token);
                                tasks.Add(ProbeAsync(address, port, throttle, token));
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    foreach (Task<IPEndPoint> task in tasks)
                    {
                        try
                        {
                            IPEndPoint endpoint = await task;
                            if (endpoint != null && !token.IsCancellationRequested)
                                AddFavorite(endpoint);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        UpdateConnections();
                    }
                }
            }
            catch (Exception exception)
            {
                Logging.Error("RDP network discovery failed.", exception);
            }
            finally
            {
                pendingRequests = 0;
                if (!IsDisposed)
                {
                    UpdateConnections();
                    if (!discoveryFinished)
                    {
                        discoveryFinished = true;
                        if (OnDiscoveryCompleted != null)
                            OnDiscoveryCompleted();
                    }
                }
                discoveryCancellation.Dispose();
                discoveryCancellation = null;
            }
        }

        private async Task<IPEndPoint> ProbeAsync(IPAddress address, int port,
            SemaphoreSlim throttle, CancellationToken token)
        {
            try
            {
                bool open = await TcpConnectScanner.IsOpenAsync(
                    address, port, TimeSpan.FromMilliseconds(1000), token).ConfigureAwait(false);
                return open ? new IPEndPoint(address, port) : null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception exception)
            {
                Logging.Info("Discovery TCP probe failed.", exception);
                return null;
            }
            finally
            {
                Interlocked.Decrement(ref pendingRequests);
                throttle.Release();
            }
        }

        internal void CancelDiscovery()
        {
            if (discoveryCancellation != null)
                discoveryCancellation.Cancel();
        }

        private void AddFavorite(IPEndPoint endPoint)
        {
            try
            {
                string protocol = this.connectionManager.GetPortName(endPoint.Port);
                string serverName = endPoint.Address.ToString();
                string connectionName = String.Format("{0}_{1}", serverName, protocol);
                FavoriteConfigurationElement newFavorite =
                    FavoritesFactory.CreateNewFavorite(this.connectionManager, connectionName, serverName, endPoint.Port);

                AddFavoriteToDiscovered(newFavorite);
            }
            catch (Exception e)
            {
                Logging.Error("Add Favorite Error", e);
            }
        }

        private void AddFavoriteToDiscovered(FavoriteConfigurationElement newFavorite)
        {
            lock (this.DiscoveredConnections)
            {
                this.DiscoveredConnections.Add(newFavorite);
            }
        }

        private void UpdateConnections()
        {
            if (IsDisposed)
                return;
            ConnectionsCountLabel.Text = DiscoveredConnections.Count.ToString();
            PendingRequestsLabel.Text = Math.Max(0, pendingRequests).ToString();
        }

        /// <summary>
        /// hidden egg to show the connections.  Just click on the connections count label to show and update the list
        /// </summary>
        private void ConnectionsCountLabel_Click(object sender, EventArgs e)
        {
            try
            {
                List<BindingElement> list = new List<BindingElement>();
                foreach (FavoriteConfigurationElement elm in this.DiscoveredConnections)
                {
                    BindingElement be = new BindingElement();
                    be.Element = string.Format("{0}:{1}", elm.ServerName, elm.Protocol);
                    list.Add(be);
                }

                this.dataGridView1.DataSource = list;
                this.dataGridView1.Visible = true;
                Application.DoEvents();
            }
            catch (Exception exc)
            {
                Logging.Info("Connections Count Label", exc);
            }
        }
    }

    /// <summary>
    /// Data grid binding class to show discovered connection name in grid
    /// </summary>
    internal class BindingElement
    {
        public string Element { get; set; }
    }
}