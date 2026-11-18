using System;
using System.Threading;
using System.Windows.Forms;

namespace Terminals.Network.WhoIs
{
    internal partial class WhoIs : UserControl
    {
        private CancellationTokenSource pending;
        private int requestVersion;

        public WhoIs()
        {
            InitializeComponent();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            CancelPending();
            base.OnHandleDestroyed(e);
        }

        private void CancelPending()
        {
            ++requestVersion;
            if (pending != null)
            {
                pending.Cancel();
                pending = null;
            }
        }

        private async void whoisButton_Click(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing) return;
            if (pending != null)
            {
                // A second click cancels the running operation.
                CancelPending();
                whoisButton.Text = "&Whois";
                return;
            }
            string domain = hostTextbox.Text.Trim();
            if (domain.Length == 0) return;

            int version = ++requestVersion;
            var cancellation = new CancellationTokenSource();
            pending = cancellation;
            whoisButton.Text = "&Cancel";
            textBox2.Clear();

            try
            {
                string result = await WhoisQuery.LookupAsync(domain, cancellation.Token);
                if (version == requestVersion && !IsDisposed && !Disposing)
                    textBox2.Text = result.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (version == requestVersion && !IsDisposed && !Disposing)
                {
                    Logging.Info("WHOIS query failed.", exception);
                    textBox2.Text = "WHOIS query failed: " + exception.Message;
                }
            }
            finally
            {
                if (version == requestVersion)
                {
                    pending = null;
                    if (!IsDisposed && !Disposing)
                        whoisButton.Text = "&Whois";
                }
                cancellation.Dispose();
            }
        }

        private void hostTextbox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                if (whoisButton.Enabled)
                    whoisButton.PerformClick();
            }
        }
    }
}
