using System;
using System.Windows.Forms;
using System.Threading.Tasks;
using Terminals.Data;

namespace Terminals.Connections
{
    internal partial class RASProperties : UserControl
    {
        public string LastError { get; set; }
        private RASConnection rASConnection;
        private Timer timer;
        private DateTime connectedTime = DateTime.MinValue;
        private string renderedStatus;
        private bool updating;

        public RASConnection RASConnection
        {
            get { return rASConnection; }
            set
            {
                if (rASConnection != null)
                    rASConnection.OnLog -= rASConnection_OnLog;
                rASConnection = value;
                if (rASConnection != null)
                    rASConnection.OnLog += rASConnection_OnLog;
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
            if (IsHandleCreated)
                timer.Start();

        }

        private async void timer_Tick(object sender, EventArgs e)
        {
            if (updating || rASConnection == null || IsDisposed || Disposing)
                return;
            updating = true;
            try
            {
                await rASConnection.RefreshStatusAsync();
                if (!IsDisposed && !Disposing)
                    UpdateStats();
            }
            finally { updating = false; }
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
            if (Connected && connectedTime == DateTime.MinValue)
                connectedTime = DateTime.Now;
            if (!Connected)
                connectedTime = DateTime.MinValue;
            string details = "Connection Status: " + rASConnection.StatusText;
            if (Connected)
                details += "\nHost: " + rASConnection.EntryName + "\nIP Address: " + rASConnection.Address +
                    "\nConnection Duration: " + (DateTime.Now - connectedTime).ToString(@"d\.hh\:mm\:ss");
            if (details == renderedStatus)
                return;
            renderedStatus = details;
            lbDetails1.BeginUpdate();
            try
            {
                lbDetails1.Items.Clear();
                lbDetails1.Items.AddRange(details.Split('\n'));
            }
            finally { lbDetails1.EndUpdate(); }
        }

        private void rASConnection_OnLog(string entry)
        {
            if (IsDisposed || Disposing)
                return;
            LogListBox.TopIndex = LogListBox.Items.Add(entry);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (timer != null)
                timer.Start();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (timer != null)
                timer.Stop();
            base.OnHandleDestroyed(e);
        }

        private void ReleaseSubscriptions()
        {
            if (rASConnection != null)
                rASConnection.OnLog -= rASConnection_OnLog;
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= timer_Tick;
                timer.Dispose();
                timer = null;
            }
        }
    }
}
