using System;
using System.Windows.Forms;
using Terminals.Connections.RasNative;
using Terminals.Data;

namespace Terminals.Connections
{
    internal partial class RASProperties : UserControl
    {
        public string LastError { get; set; }
        private RASConnection rASConnection;
        private Timer timer;
        private MethodInvoker logMiv;
        private DateTime connectedTime = DateTime.MinValue;
        private string Entry = string.Empty;
        private string phoneNumber;

        public RASConnection RASConnection
        {
            get { return rASConnection; }
            set
            {
                rASConnection = value;
                rASConnection.OnLog += new Connection.LogHandler(rASConnection_OnLog);
            }
        }

        public TerminalServices.TerminalServer Server
        {
            get { return null; }
        }

        public bool IsTerminalServer
        {
            get { return false; }
            set { }
        }

        public RASProperties()
        {
            InitializeComponent();
            timer = new Timer { Interval = 500 };
            timer.Tick += timer_Tick;
            timer.Start();
            logMiv = new MethodInvoker(UpdateLog);
        }

        private void UpdateLog()
        {
            LogListBox.TopIndex = LogListBox.Items.Add(Entry);
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            UpdateStats();
        }

        public IFavorite Favorite
        {
            get { return RASConnection.Favorite; }
            set { RASConnection.Favorite = value; }
        }

        public bool Connect()
        {
            return RASConnection.Connect();
        }

        public void Disconnect()
        {
            timer.Stop();
            RASConnection.Disconnect();
        }

        public bool Connected
        {
            get { return rASConnection != null && rASConnection.Connected; }
        }

        public void ChangeDesktopSize(DesktopSize size) { }

        private void UpdateStats()
        {
            if (rASConnection == null)
                return;

            rASConnection.NotifyIfDisconnected();
            lbDetails1.Items.Clear();
            BringToFront();
            if (Connected)
            {
                if (connectedTime == DateTime.MinValue)
                {
                    connectedTime = DateTime.Now;
                    try
                    {
                        foreach (var item in RasConnectionClient.GetEntries())
                        {
                            if (string.Equals(item.Name, rASConnection.ras.EntryName, StringComparison.OrdinalIgnoreCase))
                            {
                                phoneNumber = item.PhoneNumber;
                                break;
                            }
                        }
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        phoneNumber = string.Empty;
                    }
                }

                AddDetailsText("Connection Status", "Connected");
                AddDetailsText("Host", phoneNumber);
                AddDetailsText("IP Address", rASConnection.ras.IPAddress());
                TimeSpan ts = DateTime.Now - connectedTime;
                AddDetailsText("Connection Duration:", string.Format("{0} Days, {1} Hours, {2} Minutes, {3} Seconds",
                    ts.Days, ts.Hours, ts.Minutes, ts.Seconds));
            }
            else
            {
                AddDetailsText("Connection Status", "Not Connected");
            }
        }

        private void AddDetailsText(string caption, object item)
        {
            if (item != null)
                lbDetails1.TopIndex = lbDetails1.Items.Add(caption + ":  " + item);
        }

        private void rASConnection_OnLog(string entry)
        {
            Entry = entry;
            if (!IsHandleCreated || IsDisposed)
                return;
            if (InvokeRequired)
                BeginInvoke(logMiv);
            else
                logMiv();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            timer.Stop();
            base.OnHandleDestroyed(e);
        }
    }
}
