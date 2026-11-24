using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using SharpPcap;
using SharpPcap.LibPcap;
using Terminals.Services;

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
        private volatile bool stopRequested;
        private volatile bool captureStarted;
        private int stopScheduled;
        private int writerErrorReported;
        private bool sessionActive;
        private bool writeFailed;

        public PacketCapture()
        {
            InitializeComponent();
            previewTimer = new System.Windows.Forms.Timer { Interval = 200 };
            previewTimer.Tick += (sender, args) => DrainPreviews();
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
            if (sessionActive || IsDisposed || Disposing) return;
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
            stopRequested = false;
            captureStarted = false;
            writeFailed = false;
            Interlocked.Exchange(ref stopScheduled, 0);
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
                if (stopRequested)
                {
                    QueueStop();
                    return;
                }
                if (!string.IsNullOrWhiteSpace(filter))
                    selectedDevice.Filter = filter;
                if (stopRequested)
                {
                    QueueStop();
                    return;
                }
                if (dumpPath != null)
                {
                    var fileWriter = new CaptureFileWriterDevice(dumpPath);
                    lock (writerLock)
                        writer = fileWriter;
                    fileWriter.Open(new DeviceConfiguration());
                }
                if (stopRequested)
                {
                    QueueStop();
                    return;
                }
                selectedDevice.OnPacketArrival += OnPacketArrival;
                selectedDevice.StartCapture();
                captureStarted = true;
                if (stopRequested)
                    QueueStop();
            }
            catch (Exception exception)
            {
                Logging.Info("Failed to start packet capture.", exception);
                PostToUi(() => MessageBox.Show(this,
                    "Unable to start capture: " + exception.Message,
                    "Packet Capture", MessageBoxButtons.OK, MessageBoxIcon.Error));
                stopRequested = true;
                QueueStop();
            }
        }

        private void OnPacketArrival(object sender, SharpPcap.PacketCapture capture)
        {
            if (stopRequested) return;
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
            stopRequested = true;
            // Startup owns the device until StartCapture returns; it will arrange cleanup.
            if (captureStarted)
                QueueStop();
        }

        private void QueueStop()
        {
            if (Interlocked.Exchange(ref stopScheduled, 1) != 0) return;
            ThreadPool.QueueUserWorkItem(state => StopCaptureWorker());
        }

        private void StopCaptureWorker()
        {
            LibPcapLiveDevice selectedDevice = activeDevice;
            try
            {
                if (selectedDevice != null)
                {
                    if (captureStarted)
                    {
                        try { selectedDevice.StopCapture(); }
                        catch (Exception exception) { Logging.Info("Failed stopping packet capture.", exception); }
                    }
                    selectedDevice.OnPacketArrival -= OnPacketArrival;
                    selectedDevice.Close();
                }
            }
            catch (Exception exception)
            {
                Logging.Info("Failed closing packet capture device.", exception);
            }
            finally
            {
                lock (writerLock)
                {
                    if (writer != null)
                    {
                        try { writer.Close(); }
                        catch (Exception exception) { Logging.Info("Failed closing capture file.", exception); }
                        writer = null;
                    }
                }
                PostToUi(CaptureStopped);
            }
        }

        private void PostToUi(MethodInvoker update)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            try
            {
                BeginInvoke(new MethodInvoker(() =>
                {
                    if (!IsDisposed && !Disposing) update();
                }));
            }
            catch (InvalidOperationException) { }        }

        private void CaptureStopped()
        {
            previewTimer.Stop();
            DrainPreviews();
            sessionActive = false;
            captureStarted = false;
            activeDevice = null;
            SetCaptureControls(true);
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
}
