using System;
using System.Threading;
using System.Windows.Forms;

namespace Terminals.Network.NTP
{
    internal partial class NetworkTime : UserControl
    {
        private CancellationTokenSource pending;
        private int version;

        public NetworkTime()
        {
            InitializeComponent();
            Disposed += (sender, args) => CancelPending();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            CancelPending();
            base.OnHandleDestroyed(e);
        }

        private void CancelPending()
        {
            ++version;
            if (pending != null)
            {
                pending.Cancel();
                pending = null;
            }
        }

        private async void QueryTime()
        {
            if (IsDisposed || Disposing || pending != null) return;
            string host;
            try { host = NtpQuery.NormalizeHost(TimeServerTextBox.Text); }
            catch (ArgumentException exception)
            {
                MessageBox.Show(this, exception.Message, "NTP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int currentVersion = ++version;
            var cancellation = new CancellationTokenSource();
            pending = cancellation;
            LookupButton.Enabled = false;
            UpdateButton.Enabled = false;
            propertyGrid1.SelectedObject = null;
            try
            {
                NtpResult result = await NtpQuery.GetAsync(host, cancellation.Token);
                if (IsDisposed || Disposing || version != currentVersion) return;
                propertyGrid1.SelectedObject = result;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (IsDisposed || Disposing || version != currentVersion) return;
                Logging.Info("NTP query failed.", exception);
                MessageBox.Show(this, exception.Message, "NTP query failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (version == currentVersion)
                {
                    pending = null;
                    if (!IsDisposed && !Disposing)
                    {
                        LookupButton.Enabled = true;
                        UpdateButton.Enabled = true;
                    }
                }
                cancellation.Dispose();
            }
        }

        private void LookupButton_Click(object sender, EventArgs e) { QueryTime(); }
        // Historically Update performed another lookup; it never changed the system clock.
        private void UpdateButton_Click(object sender, EventArgs e) { QueryTime(); }

        private void NetworkTime_Load(object sender, EventArgs e)
        {
            TimeServerTextBox.Text = Unified.Network.SNTP.NTPClient.DefaultTimeServer;
        }
    }
}
