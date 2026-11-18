using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using ZedGraph;

namespace Terminals.Network
{
    internal partial class TraceRouteControl : UserControl
    {
        private readonly BindingList<TraceRouteHopData> hopList = new BindingList<TraceRouteHopData>();
        private CancellationTokenSource traceCancellation;
        private bool traceRunning;
        private int sessionVersion;
        private GraphPane myPane;

        public TraceRouteControl()
        {
            InitializeComponent();
            dataGridView1.DataSource = hopList;
            ButtonStop.Enabled = false;
            InitializeGraph();
        }

        private void TraceRoute_Load(object sender, EventArgs e) { TextHost.Focus(); }
        private void TraceRoute_Resize(object sender, EventArgs e) { SetSize(); }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            StopTrace(false);
            base.OnHandleDestroyed(e);
        }

        private async void ButtonStart_Click(object sender, EventArgs e)
        {
            if (traceRunning || IsDisposed || Disposing) return;
            string host = TextHost.Text.Trim();
            if (host.Length == 0) { TextHost.Focus(); return; }

            TextHost.Text = host;
            ButtonStart.Enabled = false;
            ButtonStop.Enabled = true;
            TextHost.Enabled = false;
            traceRunning = true;
            int version = ++sessionVersion;
            var cancellation = new CancellationTokenSource();
            traceCancellation = cancellation;
            hopList.Clear();
            UpdateGraph();

            using (var trace = new TraceRoute { Destination = host, ResolveNames = ResolveCheckBox.Checked })
            {
                try
                {
                    await trace.RunAsync(cancellation.Token, hop =>
                    {
                        if (version != sessionVersion || IsDisposed || Disposing) return;
                        hopList.Add(hop);
                        if (dataGridView1.Rows.Count > 0)
                            dataGridView1.FirstDisplayedScrollingRowIndex = dataGridView1.Rows.Count - 1;
                        UpdateGraph();
                    });
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception exception)
                {
                    if (version == sessionVersion && !IsDisposed && !Disposing)
                    {
                        Logging.Info("Traceroute failed.", exception);
                        MessageBox.Show(this, "Unable to trace " + host + ": " + exception.Message,
                            "Traceroute Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                finally
                {
                    if (version == sessionVersion)
                    {
                        traceRunning = false;
                        traceCancellation = null;
                        if (!IsDisposed && !Disposing) ResetForm();
                    }
                    cancellation.Dispose();
                }
            }
        }

        private void ButtonStop_Click(object sender, EventArgs e) { StopTrace(true); }

        private void StopTrace(bool reset)
        {
            ++sessionVersion;
            traceRunning = false;
            if (traceCancellation != null)
            {
                traceCancellation.Cancel();
                traceCancellation = null;
            }
            if (reset && !IsDisposed && !Disposing) ResetForm();
        }

        private void ResetForm()
        {
            ButtonStart.Enabled = true;
            ButtonStop.Enabled = false;
            TextHost.Enabled = true;
            TextHost.Focus();
            TextHost.SelectAll();
        }

        private void TextHost_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                e.Handled = true;
                if (!traceRunning) ButtonStart.PerformClick();
            }
        }

        public void ForceTrace(string hostName)
        {
            TextHost.Text = hostName;
            ButtonStart.PerformClick();
        }

        private void InitializeGraph()
        {
            myPane = this.ZGraph.GraphPane;
            // Set the titles and axis labels
            myPane.Title.Text = "Trace Route results";
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
            this.ZGraph.PointValueEvent += new ZedGraphControl.PointValueHandler(MyPointValueHandler);

            // OPTIONAL: Add a custom context menu item
            this.ZGraph.ContextMenuBuilder += new ZedGraphControl.ContextMenuBuilderEventHandler(MyContextMenuBuilder);

            // OPTIONAL: Handle the Zoom Event

            // Size the control to fit the window
            this.SetSize();
        }

        private void UpdateGraph()
        {
            var points = new PointPairList();
            var averages = new PointPairList();
            long sum = 0;
            long max = 8;
            for (int i = 0; i < hopList.Count; i++)
            {
                TraceRouteHopData hop = hopList[i];
                max = Math.Max(max, hop.RoundTripTime + 8);
                points.Add(hop.Count, hop.RoundTripTime,
                    hop.Address == null ? "*" : hop.Address.ToString());
                sum += hop.RoundTripTime;
                averages.Add(hop.Count, (double)sum / (i + 1));
            }
            myPane.Title.Text = "Trace Route results for " + TextHost.Text;
            myPane.YAxis.Scale.Min = 0;
            myPane.YAxis.Scale.Max = max;
            myPane.XAxis.Scale.Min = 0;
            myPane.XAxis.Scale.Max = Math.Max(1, hopList.Count + 1);
            myPane.CurveList.Clear();
            LineItem curve = myPane.AddCurve(TextHost.Text, points, Color.Blue, SymbolType.Diamond);
            myPane.AddCurve("Average", averages, Color.Red, SymbolType.Diamond);
            curve.Symbol.Fill = new Fill(Color.White);
            ZGraph.AxisChange();
            ZGraph.Invalidate();
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
                return String.Format("{0} is {1:f2} milliseconds at {2:f1}", pt.Tag, pt.Y, pt.X);
            }
            catch (Exception ex)
            {
                Logging.Error("Unable to display customized tool tips in Trace route", ex);
            }

            return String.Empty;
        }

        private void SetSize()
        {
            ZGraph.Location = new Point(10, 10);
            // Leave a small margin around the outside of the control
            ZGraph.Size = new Size(Math.Max(0, ClientRectangle.Width - 20), Math.Max(0, ClientRectangle.Height - 20));
        }

        /// <summary>
        /// Customize the context menu by adding a new item to the end of the menu
        /// </summary>
        private void MyContextMenuBuilder(ZedGraphControl control, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            //ToolStripMenuItem item = new ToolStripMenuItem();
            //item.Name = "add-beta";
            //item.Tag = "add-beta";
            //item.Text = "Add a new Beta Point";
            //item.Click += new System.EventHandler(AddBetaPoint);

            //menuStrip.Items.Add(item);
        }
    }
}
