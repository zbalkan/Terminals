using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DnsClient;

namespace Terminals.Network
{
    internal partial class DNSLookup : UserControl
    {
        private static readonly QueryType[] RecordTypes =
        {
            QueryType.A, QueryType.MX, QueryType.NS, QueryType.SOA
        };

        private CancellationTokenSource pending;
        private int sessionVersion;

        public DNSLookup()
        {
            InitializeComponent();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            CancelLookup();
            base.OnHandleDestroyed(e);
        }

        private void CancelLookup()
        {
            ++sessionVersion;
            if (pending != null)
            {
                pending.Cancel();
                pending = null;
            }
        }

        public void ForceDNS(string host)
        {
            this.hostnameTextBox.Text = host;
            this.lookupButton_Click(null, null);
        }

        internal static List<DnsLookupResult> CollectRecords(Func<QueryType, IDnsQueryResponse> query)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            var results = new List<DnsLookupResult>();
            foreach (QueryType type in RecordTypes)
            {
                try
                {
                    IDnsQueryResponse response = query(type);
                    if (response.HasError)
                    {
                        results.Add(new DnsLookupResult(type.ToString(), string.Empty, response.ErrorMessage));
                    }
                    else if (response.Answers.Count == 0)
                    {
                        results.Add(new DnsLookupResult(type.ToString(), string.Empty, "No records"));
                    }
                    else
                    {
                        foreach (var answer in response.Answers)
                        {
                            results.Add(new DnsLookupResult(type.ToString(), answer.ToString(), "OK"));
                        }
                    }
                }
                catch (DnsResponseException exception)
                {
                    results.Add(new DnsLookupResult(type.ToString(), string.Empty, exception.Message));
                }
            }
            return results;
        }

        internal static async Task<List<DnsLookupResult>> CollectRecordsAsync(
            Func<QueryType, CancellationToken, Task<IDnsQueryResponse>> query, CancellationToken cancellation)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var results = new List<DnsLookupResult>();
            foreach (QueryType type in RecordTypes)
            {
                cancellation.ThrowIfCancellationRequested();
                try
                {
                    IDnsQueryResponse response = await query(type, cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    if (response.HasError)
                        results.Add(new DnsLookupResult(type.ToString(), string.Empty, response.ErrorMessage));
                    else if (response.Answers.Count == 0)
                        results.Add(new DnsLookupResult(type.ToString(), string.Empty, "No records"));
                    else
                        foreach (var answer in response.Answers)
                            results.Add(new DnsLookupResult(type.ToString(), answer.ToString(), "OK"));
                }
                catch (DnsResponseException ex)
                {
                    cancellation.ThrowIfCancellationRequested();
                    results.Add(new DnsLookupResult(type.ToString(), string.Empty, ex.Message));
                }
            }
            return results;
        }

        private static List<string> ReadDnsServers()
        {
            var servers = new List<string>();
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up)
                    continue;
                IPInterfaceProperties properties = adapter.GetIPProperties();
                foreach (IPAddress address in properties.DnsAddresses)
                {
                    string server = address.ToString();
                    if (!servers.Contains(server))
                        servers.Add(server);
                }
            }
            return servers;
        }

        private async void lookupButton_Click(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (pending != null)
            {
                CancelLookup();
                lookupButton.Text = "Lookup";
                return;
            }

            string domain = hostnameTextBox.Text.Trim();
            if (domain.Length == 0)
            {
                MessageBox.Show(this, "Enter a domain name to query.");
                return;
            }
            string server = serverComboBox.Text.Trim();
            IPAddress serverAddress;
            if (server.Length != 0 && !IPAddress.TryParse(server, out serverAddress))
            {
                MessageBox.Show(this, "Enter a valid IPv4 or IPv6 nameserver address.");
                return;
            }

            int version = ++sessionVersion;
            var cancellation = new CancellationTokenSource();
            pending = cancellation;
            lookupButton.Text = "Cancel";
            dataGridView1.DataSource = null;
            try
            {
                LookupClient client = server.Length == 0
                    ? new LookupClient()
                    : new LookupClient(IPAddress.Parse(server), 53);
                List<DnsLookupResult> results = await CollectRecordsAsync(
                    (type, token) => client.QueryAsync(domain, type, cancellationToken: token),
                    cancellation.Token);
                if (version == sessionVersion && !IsDisposed && !Disposing)
                    dataGridView1.DataSource = results;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (version == sessionVersion && !IsDisposed && !Disposing)
                {
                    Logging.Info("Could not resolve host.", exception);
                    MessageBox.Show(this, "Could not resolve host: " + exception.Message);
                }
            }
            finally
            {
                if (version == sessionVersion)
                {
                    pending = null;
                    if (!IsDisposed && !Disposing)
                        lookupButton.Text = "Lookup";
                }
                cancellation.Dispose();
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private async void DNSLookup_Load(object sender, EventArgs e)
        {
            int version = sessionVersion;
            try
            {
                List<string> servers = await Task.Run(() => ReadDnsServers());
                if (version == sessionVersion && !IsDisposed && !Disposing)
                {
                    string previous = serverComboBox.Text;
                    serverComboBox.DataSource = servers;
                    serverComboBox.Text = previous;
                }
            }
            catch (Exception ex)
            {
                Logging.Info("Could not enumerate DNS servers.", ex);
            }
        }

        private void hostnameTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                lookupButton_Click(null, null);
        }
    }

    internal sealed class DnsLookupResult
    {
        public DnsLookupResult(string queryType, string record, string status)
        {
            QueryType = queryType;
            Record = record;
            Status = status;
        }

        public string QueryType { get; private set; }
        public string Record { get; private set; }
        public string Status { get; private set; }
    }

    public class IPRender
    {
        public System.Net.IPAddress address;
        public string Address { get { return address.ToString(); } }
        public string AddressFamily { get { return address.AddressFamily.ToString(); } }
        public bool IsIPv6LinkLocal { get { return address.IsIPv6LinkLocal; } }
        public bool IsIPv6Multicast { get { return address.IsIPv6Multicast; } }
        public bool IsIPv6SiteLocal { get { return address.IsIPv6SiteLocal; } }
    }

    public class KnownAlias
    {
        private string alias;
        public string Alias { get { return alias; } set { alias = value; } }
    }
}
