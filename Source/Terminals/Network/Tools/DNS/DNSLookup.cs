using System;
using System.Collections.Generic;
using System.Net;
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

        public DNSLookup()
        {
            InitializeComponent();
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

        private void lookupButton_Click(object sender, EventArgs e)
        {
            string serverIP = this.serverComboBox.Text.Trim();
            string domain = this.hostnameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(domain))
            {
                MessageBox.Show("Enter a domain name to query.");
                return;
            }

            try
            {
                // An empty server uses the operating system DNS configuration.
                LookupClient client = string.IsNullOrEmpty(serverIP)
                    ? new LookupClient()
                    : new LookupClient(IPAddress.Parse(serverIP), 53);

                this.dataGridView1.DataSource =
                    CollectRecords(type => client.Query(domain, type));
            }
            catch (Exception exception)
            {
                Logging.Info("Could not resolve host.", exception);
                MessageBox.Show("Could not resolve host.");
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void DNSLookup_Load(object sender, EventArgs e)
        {
            this.serverComboBox.DataSource = Terminals.Network.DNS.AdapterInfo.DNSServers;
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
