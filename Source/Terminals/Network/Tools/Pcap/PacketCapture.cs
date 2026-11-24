using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using SharpPcap;
using SharpPcap.LibPcap;

namespace Terminals
{
    public partial class PacketCapture : UserControl
    {
        private const int MaxPreviewPackets = 500;
        private readonly PacketPreviewBuffer<CapturedPacket> previews =
            new PacketPreviewBuffer<CapturedPacket>(MaxPreviewPackets);
        private readonly object writerLock = new object();
        private readonly System.Windows.Forms.Timer previewTimer;
        private LibPcapLiveDeviceList devices;
        private LibPcapLiveDevice device;
        private LibPcapLiveDevice activeDevice;
        private CaptureFileWriterDevice writer;
        private CaptureSessionLifecycle lifecycle;
        private Exception startFailure;
        private volatile bool cleanupComplete;
        private int writerErrorReported;
        private bool sessionActive;
        private bool writeFailed;
        private bool cleanupFailed;
        private bool nativeStartAttempted;

        public PacketCapture()
        {
            InitializeComponent();
            previewTimer = new System.Windows.Forms.Timer { Interval = 200 };
            previewTimer.Tick += (sender, args) =>
            {
                DrainPreviews();
                if (cleanupComplete && sessionActive) CaptureStopped();
            };
            Disposed += (sender, args) =>
            {
                previewTimer.Stop();
                previewTimer.Dispose();
                RequestStop();
            };
        }

        private void PacketCapture_Load(object sender, EventArgs e)
        {
            try
            {
                promiscuousCheckbox.Enabled = true;
                DumpToFileCheckbox.Enabled = true;
                StopCaptureButton.Enabled = false;
                AmberPicture.Visible = true;
                GreenPicture.Visible = false;
                RedPicture.Visible = false;
                devices = LibPcapLiveDeviceList.Instance;
                comboBox1.Items.Clear();
                foreach (LibPcapLiveDevice captureDevice in devices)
                    comboBox1.Items.Add(captureDevice.Description ?? captureDevice.Name);
                if (devices.Count > 0)
                    comboBox1.SelectedIndex = 0;
                webBrowser1.DocumentStream = new MemoryStream(
                    Encoding.Default.GetBytes(Properties.Resources.Filtering));
            }
            catch (Exception exception)
            {
                Enabled = false;
                Logging.Info("Packet capture device enumeration failed. Install a supported Npcap driver.", exception);
                MessageBox.Show("Unable to enumerate packet capture devices. Verify that Npcap is installed.");
            }
            PacketCapture_Resize(null, null);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (!RecreatingHandle)
                RequestStop();
            base.OnHandleDestroyed(e);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = comboBox1.SelectedIndex;
            if (sessionActive || index < 0 || devices == null || index >= devices.Count)
                return;
            device = devices[index];
            propertyGrid1.SelectedObject = device;
        }

        private void CaptureButton_Click(object sender, EventArgs e)
        {
            if (sessionActive || cleanupFailed || IsDisposed || Disposing) return;
            if (device == null)
            {
                MessageBox.Show("Select a packet capture device.");
                return;
            }

            string dumpPath = null;
            if (DumpToFileCheckbox.Checked)
            {
                using (var dialog = new SaveFileDialog
                {
                    Title = "Save packet capture",
                    Filter = "PCAP capture (*.pcap)|*.pcap|All files (*.*)|*.*",
                    DefaultExt = "pcap",
                    AddExtension = true,
                    OverwritePrompt = true,
                    FileName = "Terminals.pcap"
                })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    dumpPath = dialog.FileName;
                }
            }

            LibPcapLiveDevice selectedDevice = device;
            bool promiscuous = promiscuousCheckbox.Checked;
            string filter = FilterTextBox.Text;
            sessionActive = true;
            cleanupComplete = false;
            startFailure = null;
            writeFailed = false;
            cleanupFailed = false;
            nativeStartAttempted = false;
            lifecycle = new CaptureSessionLifecycle(QueueStop);
            Interlocked.Exchange(ref writerErrorReported, 0);
            activeDevice = selectedDevice;
            previews.Clear();
            listBox1.Items.Clear();
            hexBox1.ByteProvider = null;
            textBox1.Clear();
            treeView1.Nodes.Clear();
            SetCaptureControls(false);
            // Stop also works while device.Open or filter initialization is in progress.
            StopCaptureButton.Enabled = true;
            previewTimer.Start();

            ThreadPool.QueueUserWorkItem(state => StartCapture(selectedDevice, promiscuous, filter, dumpPath));
        }

        private void StartCapture(LibPcapLiveDevice selectedDevice, bool promiscuous,
            string filter, string dumpPath)
        {
            try
            {
                selectedDevice.Open(promiscuous ? DeviceModes.Promiscuous : DeviceModes.None, 1000);
                if (lifecycle.StopRequested)
                {
                    return;
                }
                if (!string.IsNullOrWhiteSpace(filter))
                    selectedDevice.Filter = filter;
                if (lifecycle.StopRequested)
                {
                    return;
                }
                if (dumpPath != null)
                {
                    var fileWriter = new CaptureFileWriterDevice(dumpPath);
                    lock (writerLock)
                        writer = fileWriter;
                    fileWriter.Open(new DeviceConfiguration());
                }
                if (lifecycle.StopRequested)
                {
                    return;
                }
                selectedDevice.OnPacketArrival += OnPacketArrival;
                nativeStartAttempted = true;
                selectedDevice.StartCapture();

            }
            catch (Exception exception)
            {
                Logging.Info("Failed to start packet capture.", exception);
                startFailure = exception;
                lifecycle.RequestStop();
            }
            finally
            {
                lifecycle.StartupFinished();
            }
        }

        private void OnPacketArrival(object sender, SharpPcap.PacketCapture capture)
        {
            if (lifecycle.StopRequested) return;
            try
            {
                RawCapture raw = capture.GetPacket();
                // File output is independent of the bounded UI preview history.
                lock (writerLock)
                {
                    if (writer != null && !writeFailed)
                    {
                        try { writer.Write(raw); }
                        catch (Exception exception)
                        {
                            writeFailed = true;
                            if (Interlocked.Exchange(ref writerErrorReported, 1) == 0)
                                Logging.Info("Failed writing packet capture file.", exception);
                        }
                    }
                }
                previews.Enqueue(new CapturedPacket(raw));
            }
            catch (Exception exception)
            {
                if (Interlocked.Exchange(ref writerErrorReported, 1) == 0)
                    Logging.Info("Failed processing captured packet.", exception);
            }
        }

        private void DrainPreviews()
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            List<CapturedPacket> batch = previews.Drain();
            if (batch.Count == 0) return;
            listBox1.BeginUpdate();
            try
            {
                foreach (CapturedPacket packet in batch)
                {
                    if (listBox1.Items.Count == MaxPreviewPackets)
                        listBox1.Items.RemoveAt(0);
                    listBox1.Items.Add(packet);
                }
            }
            finally
            {
                listBox1.EndUpdate();
            }
        }

        private void StopCaptureButton_Click(object sender, EventArgs e)
        {
            StopCaptureButton.Enabled = false;
            RedPicture.Visible = true;
            GreenPicture.Visible = false;
            AmberPicture.Visible = false;
            RequestStop();
        }

        private void RequestStop()
        {
            if (!sessionActive) return;
            lifecycle.RequestStop();
        }

        private void QueueStop()
        {
            ThreadPool.QueueUserWorkItem(state => StopCaptureWorker());
        }

        private void StopCaptureWorker()
        {
            LibPcapLiveDevice selectedDevice = activeDevice;
            try
            {
                if (selectedDevice != null)
                {
                    // Attempt stop even when native StartCapture failed after partial initialization.
                    try { if (nativeStartAttempted) selectedDevice.StopCapture(); }
                    catch (Exception exception) { cleanupFailed = true; Logging.Info("Failed stopping packet capture.", exception); }
                    try { selectedDevice.OnPacketArrival -= OnPacketArrival; }
                    finally { selectedDevice.Close(); }
                }
            }
            catch (Exception exception)
            {
                cleanupFailed = true;
                Logging.Info("Failed closing packet capture device.", exception);
            }
            finally
            {
                lock (writerLock)
                {
                    if (writer != null)
                    {
                        try { writer.Close(); }
                        catch (Exception exception) { writeFailed = true; Logging.Info("Failed closing capture file.", exception); }
                        writer = null;
                    }
                }
                cleanupComplete = true;
            }
        }

        private void CaptureStopped()
        {
            previewTimer.Stop();
            DrainPreviews();
            sessionActive = false;
            activeDevice = null;
            SetCaptureControls(true);
            if (cleanupFailed) CaptureButton.Enabled = false;
            if (startFailure != null)
                MessageBox.Show(this, "Unable to start capture: " + startFailure.Message,
                    "Packet Capture", MessageBoxButtons.OK, MessageBoxIcon.Error);
            if (cleanupFailed)
                MessageBox.Show(this, "Capture cleanup failed. Reopen the tool before starting another capture.",
                    "Packet Capture", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (writeFailed)
                MessageBox.Show(this, "Some packets could not be written to the capture file.",
                    "Packet Capture", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void SetCaptureControls(bool idle)
        {
            CaptureButton.Enabled = idle;
            StopCaptureButton.Enabled = false;
            DumpToFileCheckbox.Enabled = idle;
            promiscuousCheckbox.Enabled = idle;
            comboBox1.Enabled = idle;
            AmberPicture.Visible = idle;
            GreenPicture.Visible = !idle;
            RedPicture.Visible = false;
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            var packet = listBox1.SelectedItem as CapturedPacket;
            if (packet == null)
            {
                hexBox1.ByteProvider = null;
                textBox1.Clear();
                treeView1.Nodes.Clear();
                return;
            }
            hexBox1.ByteProvider = new Be.Windows.Forms.DynamicByteProvider(packet.Data);
            textBox1.Text = Encoding.Default.GetString(packet.Data);
            treeView1.Nodes.Clear();
            TreeNode header = treeView1.Nodes.Add("Capture");
            header.Nodes.Add("Length: " + packet.Data.Length);
            header.Nodes.Add("Date: " + packet.Timestamp);
            header.Nodes.Add("Link layer: " + packet.LinkLayer);
            treeView1.ExpandAll();
        }

        private void PacketCapture_Resize(object sender, EventArgs e)
        {
            hexBox1.BytesPerLine = hexBox1.Width > 640 ? 16 : 8;
        }

        private sealed class CapturedPacket
        {
            public readonly byte[] Data;
            public readonly DateTime Timestamp;
            public readonly string LinkLayer;

            public CapturedPacket(RawCapture capture)
            {
                Data = capture.Data;
                Timestamp = capture.Timeval.Date;
                LinkLayer = capture.LinkLayerType.ToString();
            }

            public override string ToString()
            {
                return Timestamp.ToString("HH:mm:ss.fff") + "  " + Data.Length + " bytes";
            }
        }
    }

    // The capture callback never schedules per-packet UI work or grows preview memory
    // without a bound. The capture file still receives all packets unless I/O fails.
    internal sealed class PacketPreviewBuffer<T>
    {
        private readonly Queue<T> queue = new Queue<T>();
        private readonly int capacity;

        internal PacketPreviewBuffer(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }

        internal void Enqueue(T item)
        {
            lock (queue)
            {
                if (queue.Count == capacity)
                    queue.Dequeue();
                queue.Enqueue(item);
            }
        }

        internal List<T> Drain()
        {
            lock (queue)
            {
                var items = new List<T>(queue);
                queue.Clear();
                return items;
            }
        }

        internal void Clear()
        {
            lock (queue)
                queue.Clear();
        }
    }

    // Native startup retains ownership until it returns. Stop is scheduled exactly once.
    internal sealed class CaptureSessionLifecycle
    {
        private readonly object sync = new object();
        private readonly Action scheduleCleanup;
        private bool stopRequested;
        private bool startupFinished;
        private bool cleanupScheduled;

        internal CaptureSessionLifecycle(Action scheduleCleanup)
        {
            this.scheduleCleanup = scheduleCleanup ?? throw new ArgumentNullException(nameof(scheduleCleanup));
        }

        internal bool StopRequested
        {
            get { lock (sync) return stopRequested; }
        }

        internal void RequestStop()
        {
            bool schedule;
            lock (sync)
            {
                stopRequested = true;
                schedule = TryScheduleCleanup();
            }
            if (schedule) scheduleCleanup();
        }

        internal void StartupFinished()
        {
            bool schedule;
            lock (sync)
            {
                startupFinished = true;
                schedule = TryScheduleCleanup();
            }
            if (schedule) scheduleCleanup();
        }

        private bool TryScheduleCleanup()
        {
            if (!stopRequested || !startupFinished || cleanupScheduled) return false;
            cleanupScheduled = true;
            return true;
        }
    }

}
