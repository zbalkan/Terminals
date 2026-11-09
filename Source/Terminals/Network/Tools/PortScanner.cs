using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Terminals.Network
{
    internal partial class PortScanner : UserControl
    {
        private CancellationTokenSource scanCancellation;
        private readonly List<ScanResult> results = new List<ScanResult>();
        private int pending;

        public PortScanner()
        {
            InitializeComponent();
            StopButton.Enabled = false;
        }

        private static bool TryOctet(string text, out int value)
        {
            return int.TryParse(text, out value) && value >= 0 && value <= 255;
        }

        private bool TryGetParameters(out List<IPAddress> addresses, out int firstPort, out int lastPort)
        {
            addresses = new List<IPAddress>();
            firstPort = lastPort = 0;
            int first, second, third, firstHost, lastHost;
            if (!TryOctet(a.Text, out first) || !TryOctet(b.Text, out second) ||
                !TryOctet(c.Text, out third) || !TryOctet(d.Text, out firstHost) ||
                !TryOctet(e.Text, out lastHost) ||
                !int.TryParse(pa.Text, out firstPort) || !int.TryParse(pb.Text, out lastPort) ||
                firstPort < 1 || firstPort > 65535 || lastPort < 1 || lastPort > 65535)
                return false;

            if (firstHost > lastHost)
            {
                int old = firstHost; firstHost = lastHost; lastHost = old;
            }
            if (firstPort > lastPort)
            {
                int old = firstPort; firstPort = lastPort; lastPort = old;
            }
            long work = (long)(lastHost - firstHost + 1) * (lastPort - firstPort + 1);
            // Avoid flooding arbitrary networks or creating unbounded numbers of tasks.
            if (work > 4096)
                return false;
            for (int host = firstHost; host <= lastHost; host++)
                addresses.Add(new IPAddress(new[] { (byte)first, (byte)second, (byte)third, (byte)host }));
            return true;
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            List<IPAddress> addresses;
            int firstPort, lastPort;
            if (!TryGetParameters(out addresses, out firstPort, out lastPort))
            {
                MessageBox.Show("Enter valid IPv4 addresses and ports (1-65535), with no more than 4096 probes.");
                return;
            }

            scanCancellation = new CancellationTokenSource();
            CancellationToken token = scanCancellation.Token;
            StartButton.Enabled = false;
            StopButton.Enabled = true;
            results.Clear();
            pending = addresses.Count * (lastPort - firstPort + 1);
            UpdateConnections();

            var progress = new Progress<ScanResult>(result =>
            {
                if (IsDisposed) return;
                if (result != null) results.Add(result);
                pending--;
                UpdateConnections();
            });
            var tasks = new List<Task>();
            using (var throttle = new SemaphoreSlim(32))
            {
                try
                {
                    foreach (IPAddress address in addresses)
                    {
                        for (int port = firstPort; port <= lastPort; port++)
                        {
                            await throttle.WaitAsync(token);
                            int targetPort = port;
                            IPAddress targetAddress = address;
                            tasks.Add(ScanOneAsync(targetAddress, targetPort, throttle, token, progress));
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException)
                {
                }
            }
            if (!IsDisposed)
            {
                StartButton.Enabled = true;
                StopButton.Enabled = false;
                if (token.IsCancellationRequested)
                    pending = 0;
                UpdateConnections();
            }
            scanCancellation.Dispose();
            scanCancellation = null;
        }

        private static async Task ScanOneAsync(IPAddress address, int port, SemaphoreSlim throttle,
            CancellationToken token, IProgress<ScanResult> progress)
        {
            try
            {
                bool open = await TcpConnectScanner.IsOpenAsync(
                    address, port, TimeSpan.FromMilliseconds(1000), token);
                progress.Report(open
                    ? new ScanResult { RemoteEndPoint = new IPEndPoint(address, port), State = "Open" }
                    : null);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Logging.Info("TCP connect scan failed.", exception);
                progress.Report(null);
            }
            finally
            {
                throttle.Release();
            }
        }

        private void PortScanner_Load(object sender, EventArgs eargs)
        {
            try
            {
                IPAddress address = NetworkInterfaceInventory.GetFirstActiveIPv4();
                if (address == null) return;
                byte[] bytes = address.GetAddressBytes();
                a.Text = bytes[0].ToString();
                b.Text = bytes[1].ToString();
                c.Text = bytes[2].ToString();
                d.Text = bytes[3].ToString();
                e.Text = bytes[3].ToString();
            }
            catch (Exception exception)
            {
                Logging.Error("Unable to enumerate network interfaces.", exception);
            }
        }

        private void UpdateConnections()
        {
            resultsGridView.Rows.Clear();
            if (resultsGridView.Columns.Count == 0)
            {
                resultsGridView.Columns.Add("EndPoint", "End Point");
                resultsGridView.Columns.Add("State", "State");
            }
            foreach (ScanResult result in results)
                resultsGridView.Rows.Add(result.RemoteEndPoint, result.State);
            ScanResultsLabel.Text = string.Format("Outstanding requests: {0}", pending);
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            if (scanCancellation != null)
                scanCancellation.Cancel();
            StopButton.Enabled = false;
        }

        private void A_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.OemPeriod) b.Focus();
        }
        private void B_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.OemPeriod) c.Focus();
        }
        private void C_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.OemPeriod) d.Focus();
        }
        private void D_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.OemPeriod) this.e.Focus();
        }
        private void E_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.OemPeriod) pa.Focus();
        }
        private void Pa_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.OemPeriod) pb.Focus();
        }
        private void Pb_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) StartButton_Click(null, null);
        }

        private void CopyRemoteAddressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (resultsGridView.SelectedCells.Count == 0) return;
            int row = resultsGridView.SelectedCells[0].RowIndex;
            if (row < 0 || row >= resultsGridView.Rows.Count) return;
            object value = resultsGridView.Rows[row].Cells[0].Value;
            var endpoint = value as IPEndPoint;
            if (endpoint != null)
                Clipboard.SetText(endpoint.Address.ToString(), TextDataFormat.Text);
        }
    }

    internal sealed class ScanResult
    {
        public IPEndPoint RemoteEndPoint { get; set; }
        public string State { get; set; }
    }
}
