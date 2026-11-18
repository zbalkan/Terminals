using System;
using System.ComponentModel;
using System.Drawing;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZedGraph;

namespace Terminals.Network
{
    internal partial class Ping : UserControl
    {
        private readonly byte[] buffer = Encoding.ASCII.GetBytes(new string('.', 32));
        private readonly PingOptions packetOptions = new PingOptions(50, true);
        private readonly PingSampleHistory history = new PingSampleHistory(600);
        private GraphPane myPane;
        private System.Net.NetworkInformation.Ping pingSender;
        private System.Windows.Forms.Timer sendTimer;
        private System.Windows.Forms.Timer graphTimer;
        private IPAddress destinationAddress;
        private string destination = string.Empty;
        private string hostName = string.Empty;
        private long counter;
        private int requestVersion;
        private bool resolving;
        private bool pingRunning;
        private bool pingReady;
        private bool graphDirty;

        internal Ping()
        {
            InitializeComponent();
            dataGridView1.DataSource = history.Rows;
            ButtonStop.Enabled = false;
            InitializeGraph();
            Disposed += (sender, args) => StopPing(false);
        }

        internal static IPAddress SelectPingAddress(IPAddress[] addresses)
        {
            if (addresses == null || addresses.Length == 0)
                throw new ArgumentException("DNS returned no addresses.", nameof(addresses));
            return addresses[0];
        }

        private void Ping_Load(object sender, EventArgs e)
        {
            TextHost.Focus();
        }

        private void Ping_Resize(object sender, EventArgs e)
        {
            SetSize();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            StopPing(false);
            base.OnHandleDestroyed(e);
        }

        internal void ForcePing(string host)
        {
            TextHost.Text = host;
            ButtonStart.PerformClick();
        }

        private async void ButtonStart_Click(object sender, EventArgs e)
        {
            if (IsDisposed || pingRunning || resolving) return;

            string host = TextHost.Text.Trim();
            if (host.Length == 0)
            {
                TextHost.Focus();
                return;
            }

            TextHost.Text = host;
            ButtonStart.Enabled = false;
            ButtonStop.Enabled = true;
            TextHost.Enabled = false;
            resolving = true;
            int version = ++requestVersion;

            try
            {
                // Name resolution must never block WinForms' UI message pump.
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(host);
                if (IsDisposed || version != requestVersion) return;
                destinationAddress = SelectPingAddress(addresses);
                destination = destinationAddress.ToString();

                IPAddress literal;
                hostName = IPAddress.TryParse(host, out literal)
                    ? destination : (await Dns.GetHostEntryAsync(host)).HostName;
                if (IsDisposed || version != requestVersion) return;

                history.Clear();
                counter = 1;
                graphDirty = true;
                UpdateGraph();

                pingSender = new System.Net.NetworkInformation.Ping();
                pingRunning = true;
                pingReady = true;
                sendTimer = new System.Windows.Forms.Timer { Interval = (int)DelayNumericUpDown.Value };
                sendTimer.Tick += SendTimer_Tick;
                graphTimer = new System.Windows.Forms.Timer { Interval = 300 };
                graphTimer.Tick += GraphTimer_Tick;
                sendTimer.Start();
                graphTimer.Start();
                SendPingAsync(version);
            }
            catch (Exception exception)
            {
                if (!IsDisposed && version == requestVersion)
                {
                    Logging.Info("Unable to initialize ping.", exception);
                    MessageBox.Show(this, "Unable to ping " + host + ": " + exception.Message,
                        "Ping Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                if (!IsDisposed && version == requestVersion)
                {
                    resolving = false;
                    if (!pingRunning)
                        ResetForm();
                }
            }
        }

        private void SendTimer_Tick(object sender, EventArgs e)
        {
            if (!pingRunning || !pingReady) return;
            int interval = (int)DelayNumericUpDown.Value;
            if (sendTimer.Interval != interval)
                sendTimer.Interval = interval;
            SendPingAsync(requestVersion);
        }

        private void GraphTimer_Tick(object sender, EventArgs e)
        {
            if (graphDirty)
                UpdateGraph();
        }

        private async void SendPingAsync(int version)
        {
            if (!pingRunning || !pingReady || IsDisposed || pingSender == null) return;
            pingReady = false;
            try
            {
                // Await resumes on the WinForms synchronization context.
                PingReply reply = await pingSender.SendPingAsync(destinationAddress, 4000, buffer, packetOptions);
                if (!pingRunning || version != requestVersion || IsDisposed) return;
                PingReplyData sample;
                if (reply.Status == IPStatus.Success)
                {
                    sample = new PingReplyData(counter++, "Reply from: ", hostName, destination,
                        reply.Buffer.Length, reply.Options == null ? 0 : reply.Options.Ttl,
                        reply.RoundtripTime);
                }
                else
                {
                    string status = reply.Status == IPStatus.TimedOut ? "Request timed out."
                        : reply.Status == IPStatus.DestinationHostUnreachable
                            ? "Destination host unreachable." : reply.Status.ToString();
                    sample = new PingReplyData(counter++, status, string.Empty, string.Empty, 0, 0, 0);
                }

                history.Add(sample);
                if (dataGridView1.Rows.Count > 0)
                    dataGridView1.FirstDisplayedScrollingRowIndex = dataGridView1.Rows.Count - 1;
                graphDirty = true;
            }
            catch (Exception exception)
            {
                if (pingRunning && version == requestVersion && !IsDisposed)
                    Logging.Info("Ping request failed.", exception);
            }
            finally
            {
                if (version == requestVersion && pingRunning)
                    pingReady = true;
            }
        }

        private void ButtonStop_Click(object sender, EventArgs e)
        {
            StopPing(true);
        }

        private void StopPing(bool resetControls)
        {
            ++requestVersion;
            resolving = false;
            pingRunning = false;
            pingReady = false;
            if (sendTimer != null)
            {
                sendTimer.Stop();
                sendTimer.Dispose();
                sendTimer = null;
            }
            if (graphTimer != null)
            {
                graphTimer.Stop();
                graphTimer.Dispose();
                graphTimer = null;
            }
            if (pingSender != null)
            {
                pingSender.Dispose();
                pingSender = null;
            }
            if (resetControls && !IsDisposed && !Disposing)
            {
                if (graphDirty) UpdateGraph();
                ResetForm();
            }
        }

        private void TextHost_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                e.Handled = true;
                if (!pingRunning && !resolving)
                    ButtonStart.PerformClick();
            }
        }

        private void ResetForm()
        {
            ButtonStart.Enabled = true;
            ButtonStop.Enabled = false;
            TextHost.Enabled = true;
            TextHost.Focus();
            TextHost.SelectAll();
        }

        // Graph rendering is limited to the recent, bounded history and coalesced at 300 ms.
        private void InitializeGraph()
        {
            myPane = this.ZGraph.GraphPane;
            // Set the titles and axis labels
            myPane.Title.Text = "Ping results";
            myPane.XAxis.Title.Text = "Counter";
            myPane.YAxis.Title.Text = "Time, Milliseconds";

            // Show the x axis grid
            myPane.XAxis.MajorGrid.IsVisible = true;

            // Make the Y axis scale red
            myPane.YAxis.Scale.FontSpec.FontColor = Color.Blue;
            myPane.YAxis.Title.FontSpec.FontColor = Color.Blue;
            // turn off the opposite tics so the Y tics don't show up on the Y2 axis
            myPane.YAxis.MajorTic.IsOpposite = false;
            myPane.YAxis.MinorTic.IsOpposite = false;
            // Don't display the Y zero line
            myPane.YAxis.MajorGrid.IsZeroLine = false;
            // Align the Y axis labels so they are flush to the axis
            myPane.YAxis.Scale.Align = AlignP.Inside;

            // Fill the axis background with a gradient
            myPane.Chart.Fill = new Fill(Color.White, Color.LightGray, 45.0f);

            // Add a text box with instructions
            TextObj text = new TextObj(
                "Zoom: left mouse & drag\nPan: middle mouse & drag\nContext Menu: right mouse",
                0.02f, 0.15f, CoordType.ChartFraction, AlignH.Left, AlignV.Bottom);
            text.FontSpec.Size = 8;
            text.FontSpec.StringAlignment = StringAlignment.Near;
            myPane.GraphObjList.Add(text);

            // Enable scrollbars if needed
            this.ZGraph.IsShowHScrollBar = true;
            this.ZGraph.IsShowVScrollBar = true;

            // OPTIONAL: Show tooltips when the mouse hovers over a point
            this.ZGraph.IsShowPointValues = true;
            this.ZGraph.PointValueEvent += new ZedGraphControl.PointValueHandler(this.MyPointValueHandler);

            // OPTIONAL: Add a custom context menu item
            //this.ZGraph.ContextMenuBuilder += new ZedGraphControl.ContextMenuBuilderEventHandler(this.MyContextMenuBuilder);

            // Size the control to fit the window
            this.SetSize();
        }

        private void UpdateGraph()
        {
            graphDirty = false;
            var roundTripList = new PointPairList();
            var avgList = new PointPairList();
            long total = 0;
            long maximum = 8;

            for (int i = 0; i < history.Rows.Count; i++)
            {
                PingReplyData sample = history.Rows[i];
                total += sample.RoundTripTime;
                roundTripList.Add(sample.Count, sample.RoundTripTime);
                avgList.Add(sample.Count, (double)total / (i + 1));
                maximum = Math.Max(maximum, sample.RoundTripTime + 8);
            }

            myPane.Title.Text = "Ping results for " + TextHost.Text;
            myPane.YAxis.Scale.Min = 0;
            myPane.YAxis.Scale.Max = maximum;
            myPane.XAxis.Scale.Min = history.Rows.Count == 0 ? 0 : history.Rows[0].Count;
            myPane.XAxis.Scale.Max = history.Rows.Count == 0 ? 1 :
                history.Rows[history.Rows.Count - 1].Count + 1;

            myPane.CurveList.Clear();
            LineItem line = myPane.AddCurve(TextHost.Text, roundTripList, Color.Blue, SymbolType.Circle);
            myPane.AddCurve("Average", avgList, Color.Red, SymbolType.Square);
            line.Symbol.Fill = new Fill(Color.White);
            ZGraph.AxisChange();
            ZGraph.Invalidate();
        }

        private void SetSize()
        {
            ZGraph.Location = new Point(10, 10);
            // Leave a small margin around the outside of the control
            ZGraph.Size = new Size(Math.Max(0, ClientRectangle.Width - 20), Math.Max(0, ClientRectangle.Height - 20));
        }

        /// <summary>
        /// Display customized tooltips when the mouse hovers over a point
        /// </summary>
        private string MyPointValueHandler(ZedGraphControl control, GraphPane pane, CurveItem curve, int iPt)
        {
            try
            {
                // Get the PointPair that is under the mouse
                PointPair pt = curve[iPt];

                return String.Format("{0} is {1:f2} milliseconds at {2:f1}", curve.Label.Text, pt.Y, pt.X);
            }
            catch (Exception ex)
            {
                Logging.Info(String.Empty, ex);
            }
            
            return String.Empty;
        }

    }

    // Keeps the bound grid and chart bounded without rebinding the grid per reply.
    internal sealed class PingSampleHistory
    {
        private readonly int capacity;
        internal BindingList<PingReplyData> Rows { get; } = new BindingList<PingReplyData>();

        internal PingSampleHistory(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }

        internal void Add(PingReplyData sample)
        {
            if (sample == null)
                throw new ArgumentNullException(nameof(sample));
            if (Rows.Count == capacity)
                Rows.RemoveAt(0);
            Rows.Add(sample);
        }

        internal void Clear()
        {
            Rows.Clear();
        }
    }
}
