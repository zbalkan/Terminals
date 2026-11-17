using System;
using System.Collections.Generic;
using System.Management;
using System.Windows.Forms;
using System.Threading.Tasks;
using Terminals.Network.WMI;

namespace Terminals.Network
{
    internal partial class NetworkShares : UserControl
    {
        public NetworkShares()
        {
            InitializeComponent();
        }

        private bool busy;

        private async void LoadShares(string username, string password, string computer)
        {
            if (busy || IsDisposed) return;
            busy = true;
            try
            {
                string cacheHost = RemoteWmiAvailability.EffectiveHost(computer, username, password);
                if (!RemoteWmiAvailability.AllowAttempt(cacheHost, this)) return;

                connectButton.Enabled = false;
                connectButton.Text = "Connecting...";
                try
                {
                    var shares = await Task.Run(() => TryLoadShares(username, password, computer));
                    if (IsDisposed) return;
                    RemoteWmiAvailability.Succeeded(cacheHost);
                    dataGridView1.DataSource = shares;
                }
                catch (Exception exception)
                {
                    RemoteWmiAvailability.Failed(cacheHost, exception);
                    if (!IsDisposed)
                        MessageBox.Show(this, "Unable to load shares: " + exception.Message, "Loading shares",
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                busy = false;
                if (!IsDisposed)
                {
                    connectButton.Enabled = true;
                    connectButton.Text = "Connect";
                }
            }
        }

        private static List<Share> TryLoadShares(string username, string password, string computer)
        {
            var shares = new List<Share>();
            using (ManagementObjectSearcher searcher = CreateSearcher(username, password, computer))
            using (ManagementObjectCollection collection = searcher.Get())
            {
                foreach (ManagementObject share in collection)
                {
                    using (share)
                    {
                        var s = new Share();
                        FillShareByManagementObject(share, s);
                        shares.Add(s);
                    }
                }
            }
            return shares;
        }

        private static void FillShareByManagementObject(ManagementObject share, Share s)
        {
            foreach (PropertyData property in share.Properties)
            {
                switch (property.Name)
                {
                    case "AccessMask":
                        s.AccessMask = ResolvePropertyValue(property);
                        break;
                    case "MaximumAllowed":
                        s.MaximumAllowed = ResolvePropertyValue(property);
                        break;
                    case "InstallDate":
                        s.InstallDate = ResolvePropertyValue(property);
                        break;
                    case "Description":
                        s.Description = ResolvePropertyValue(property);
                        break;
                    case "Caption":
                        s.Caption = ResolvePropertyValue(property);
                        break;
                    case "AllowMaximum":
                        s.AllowMaximum = ResolvePropertyValue(property);
                        break;
                    case "Name":
                        s.Name = ResolvePropertyValue(property);
                        break;
                    case "Path":
                        s.Path = ResolvePropertyValue(property);
                        break;
                    case "Status":
                        s.Status = ResolvePropertyValue(property);
                        break;
                    case "Type":
                        s.TypeId = ResolvePropertyValue(property);
                        break;
                }
            }
        }

        private static string ResolvePropertyValue(PropertyData property)
        {
            if (property.Value != null)
                return property.Value.ToString();
            return string.Empty;
        }

        private static ManagementObjectSearcher CreateSearcher(string username, string password, string computer)
        {
            var query = new ObjectQuery("select * from win32_share");
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) ||
                string.IsNullOrEmpty(computer) || computer.StartsWith(@"\\localhost"))
                return new ManagementObjectSearcher(query);

            return CreateRemoteSearcher(username, password, computer, query);
        }

        private static ManagementObjectSearcher CreateRemoteSearcher(string username, string password,
            string computer, ObjectQuery query)
        {
            var oConn = new ConnectionOptions();
            oConn.Username = username;
            oConn.Password = password;
            if (!computer.StartsWith(@"\\"))
                computer = @"\\" + computer;
            if (!computer.ToLower().EndsWith(@"\root\cimv2"))
                computer = computer + @"\root\cimv2";
            var oMs = new ManagementScope(computer, oConn);
            return new ManagementObjectSearcher(oMs, query);
        }

        private void NetworkShares_Load(object sender, EventArgs e)
        {
            this.LoadShares(string.Empty, string.Empty, string.Empty);
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            this.LoadShares(this.wmiServerCredentials1.Username, this.wmiServerCredentials1.Password, this.wmiServerCredentials1.SelectedServer);
        }

        private void WmiServerCredentials1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                this.ConnectButton_Click(this, EventArgs.Empty);
            }
        }
    }
}
