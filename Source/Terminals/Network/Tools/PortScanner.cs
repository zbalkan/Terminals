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
        private int pending;
        private bool closing;

        public PortScanner()
        {
            InitializeComponent();
            StopButton.Enabled = false;
            Disposed += (sender, args) =>
            {
                closing = true;
                if (scanCancellation != null) scanCancellation.Cancel();
            };
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
            if (closing || scanCancellation != null) return;
            List<IPAddress> addresses;
            int firstPort, lastPort;
            if (!TryGetParameters(out addresses, out firstPort, out lastPort))
            {
                MessageBox.Show("Enter valid IPv4 addresses and ports (1-65535), with no more than 4096 probes.");
                return;
            }

            string scope = string.Format("{0}–{1}, TCP ports {2}–{3} ({4} probes)", addresses[0], addresses[addresses.Count - 1], firstPort, lastPort, addresses.Count * (lastPort - firstPort + 1));
            if (MessageBox.Show(this, "Start TCP connect scan of " + scope + "?", "Confirm scan scope", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            var cancellation = new CancellationTokenSource();
            scanCancellation = cancellation;
            try
            {
                StartButton.Enabled = false;
                StopButton.Enabled = true;
                pending = addresses.Count * (lastPort - firstPort + 1);
                InitializeResults();
                UpdatePending();

                await PortScanSession.RunAsync(addresses, firstPort, lastPort,
                    (address, port, token) => TcpConnectScanner.IsOpenAsync(
                        address, port, TimeSpan.FromMilliseconds(1000), token),
                    result =>
                    {
                        if (closing || IsDisposed || Disposing ||
                            !ReferenceEquals(scanCancellation, cancellation)) return;
                        if (result != null)
                            resultsGridView.Rows.Add(result.RemoteEndPoint, result.State);
                        if (pending > 0) pending--;
                        UpdatePending();
                    }, cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Logging.Info("TCP scan failed.", exception);
            }
            finally
            {
                if (ReferenceEquals(scanCancellation, cancellation))
                    scanCancellation = null;
                cancellation.Dispose();
                if (!closing && !IsDisposed && !Disposing)
                {
                    pending = 0;
                    UpdatePending();
                    StopButton.Enabled = false;
                    StartButton.Enabled = true;
                }
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

        private void InitializeResults()
        {
            resultsGridView.Rows.Clear();
            if (resultsGridView.Columns.Count == 0)
            {
                resultsGridView.Columns.Add("EndPoint", "End Point");
                resultsGridView.Columns.Add("State", "State");
            }
        }

        private void UpdatePending()
        {
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
            if (e.KeyCode == Keys.Enter && StartButton.Enabled && scanCancellation == null)
                StartButton_Click(sender, e);
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

    // Called from the UI context; result delivery completes before the session task.
    internal static class PortScanSession
    {
        internal static async Task RunAsync(IReadOnlyList<IPAddress> addresses,
            int firstPort, int lastPort,
            Func<IPAddress, int, CancellationToken, Task<bool>> probe,
            Action<ScanResult> report, CancellationToken token)
        {
            if (addresses == null) throw new ArgumentNullException(nameof(addresses));
            if (probe == null) throw new ArgumentNullException(nameof(probe));
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (firstPort < 1 || lastPort > 65535 || firstPort > lastPort)
                throw new ArgumentOutOfRangeException(nameof(firstPort));
            if ((long)addresses.Count * (lastPort - firstPort + 1) > 4096)
                throw new ArgumentOutOfRangeException(nameof(addresses));

            var tasks = new List<Task>();
            using (var throttle = new SemaphoreSlim(32))
            {
                try
                {
                    foreach (IPAddress address in addresses)
                    {
                        for (int port = firstPort; port <= lastPort; port++)
                        {
                            token.ThrowIfCancellationRequested();
                            await throttle.WaitAsync(token);
                            tasks.Add(ProbeAsync(address, port, probe, report, throttle, token));
                        }
                    }
                }
                finally
                {
                    await Task.WhenAll(tasks);
                }
            }
            token.ThrowIfCancellationRequested();
        }

        private static async Task ProbeAsync(IPAddress address, int port,
            Func<IPAddress, int, CancellationToken, Task<bool>> probe,
            Action<ScanResult> report, SemaphoreSlim throttle, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                bool open;
                try
                {
                    open = await probe(address, port, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    Logging.Info("TCP connect scan failed.", exception);
                    open = false;
                }
                token.ThrowIfCancellationRequested();
                report(open ? new ScanResult
                {
                    RemoteEndPoint = new IPEndPoint(address, port),
                    State = "Open"
                } : null);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            finally
            {
                throttle.Release();
            }
        }
    }

}
