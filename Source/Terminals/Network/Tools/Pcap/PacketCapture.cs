using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using SharpPcap;
using SharpPcap.LibPcap;
using Terminals.Services;

namespace Terminals
{
    public partial class PacketCapture : UserControl
    {
        private readonly List<CapturedPacket> packets = new List<CapturedPacket>();
        private readonly List<CapturedPacket> pendingPackets = new List<CapturedPacket>();
        private LibPcapLiveDeviceList devices;
        private LibPcapLiveDevice device;
        private CaptureFileWriterDevice writer;
        private readonly System.Windows.Forms.Timer updateTimer = new System.Windows.Forms.Timer { Interval = 200 };
        private const int MaxPackets = 2000;
        private string dumpFile;
        private volatile bool stopRequested;
        private volatile bool captureStarting;
        private volatile bool captureActive;
        private volatile bool closing;
        private int stopQueued;

        public PacketCapture()
        {
            InitializeComponent();
            updateTimer.Tick += (sender, args) => UpdateUI();
            Disposed += (sender, args) =>
            {
                closing = true;
                stopRequested = true;
                updateTimer.Stop();
                updateTimer.Dispose();
                if (captureActive) QueueStop(device, false);
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
                this.webBrowser1.DocumentStream = new System.IO.MemoryStream(
                    Encoding.Default.GetBytes(Properties.Resources.Filtering));
            }
            catch (Exception exception)
            {
                this.Enabled = false;
                Logging.Info("Packet capture device enumeration failed. Install a supported Npcap driver.", exception);
                MessageBox.Show("Unable to enumerate packet capture devices. Verify that Npcap is installed.");
            }
            this.PacketCapture_Resize(null, null);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = comboBox1.SelectedIndex;
            if (index < 0 || devices == null || index >= devices.Count)
                return;
            device = devices[index];
            propertyGrid1.SelectedObject = device;
        }

        private void CaptureButton_Click(object sender, EventArgs e)
        {
            if (captureStarting || captureActive || closing) return;
            if (device == null)
            {
                MessageBox.Show("Select a packet capture device.");
                return;
            }

            var selectedDevice = device;
            bool promiscuous = promiscuousCheckbox.Checked;
            bool saveToFile = DumpToFileCheckbox.Checked;
            string filter = FilterTextBox.Text;
            string selectedDumpFile = null;
            if (saveToFile)
            {
                using (var dialog = new SaveFileDialog { Filter = "Packet captures (*.pcap)|*.pcap|All files (*.*)|*.*", DefaultExt = "pcap", AddExtension = true, OverwritePrompt = true })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    selectedDumpFile = dialog.FileName;
                }
            }
            dumpFile = selectedDumpFile;
            stopRequested = false;
            System.Threading.Interlocked.Exchange(ref stopQueued, 0);
            captureStarting = true;
            updateTimer.Start();

            DumpToFileCheckbox.Enabled = false;
            promiscuousCheckbox.Enabled = false;
            comboBox1.Enabled = false;
            CaptureButton.Enabled = false;
            // Permit cancellation while opening the capture device.
            StopCaptureButton.Enabled = true;
            AmberPicture.Visible = false;
            GreenPicture.Visible = true;
            RedPicture.Visible = false;
            listBox1.Items.Clear();
            lock (packets)
            {
                packets.Clear();
                pendingPackets.Clear();
            }

            System.Threading.ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    selectedDevice.Open(
                        promiscuous ? DeviceModes.Promiscuous : DeviceModes.None, 1000);
                    if (!string.IsNullOrWhiteSpace(filter))
                        selectedDevice.Filter = filter;
                    if (saveToFile)
                    {
                        writer = new CaptureFileWriterDevice(selectedDumpFile);
                        writer.Open(new DeviceConfiguration());
                    }
                    selectedDevice.OnPacketArrival += OnPacketArrival;
                    selectedDevice.StartCapture();
                    captureActive = true;
                    captureStarting = false;
                    if (stopRequested || closing)
                        QueueStop(selectedDevice, saveToFile && !closing);
                }
                catch (Exception exception)
                {
                    Logging.Info("Failed to start packet capture.", exception);
                    captureStarting = false;
                    captureActive = false;
                    try { selectedDevice.OnPacketArrival -= OnPacketArrival; selectedDevice.Close(); } catch (Exception) { }
                    lock (packets)
                    {
                        if (writer != null) { try { writer.Close(); } catch (Exception) { } writer = null; }
                    }
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke(new MethodInvoker(() =>
                        {
                            MessageBox.Show("Unable to start capture: " + exception.Message);
                            CaptureStopped();
                        }));
                }
            });
        }

        private void OnPacketArrival(object sender, SharpPcap.PacketCapture capture)
        {
            RawCapture raw = capture.GetPacket();
            var entry = new CapturedPacket(raw);
            lock (packets)
            {
                if (closing || stopRequested) return;
                packets.Add(entry);
                pendingPackets.Add(entry);
                if (writer != null)
                {
                    try { writer.Write(raw); }
                    catch (Exception exception) { Logging.Info("Unable to write captured packet.", exception); }
                }
            }
        }

        private void UpdateUI()
        {
            lock (packets)
            {
                if (closing || IsDisposed) return;
                listBox1.BeginUpdate();
                try
                {
                foreach (CapturedPacket packet in pendingPackets)
                    listBox1.Items.Add(packet);
                pendingPackets.Clear();
                int excess = packets.Count - MaxPackets;
                if (excess > 0)
                {
                    packets.RemoveRange(0, excess);
                    for (int i = 0; i < excess && listBox1.Items.Count > 0; i++)
                        listBox1.Items.RemoveAt(0);
                }
                }
                finally { listBox1.EndUpdate(); }
            }
        }

        private void StopCaptureButton_Click(object sender, EventArgs e)
        {
            stopRequested = true;
            if (captureStarting)
            {
                StopCaptureButton.Enabled = false;
                return;
            }
            CaptureButton.Enabled = false;
            StopCaptureButton.Enabled = false;
            RedPicture.Visible = true;
            GreenPicture.Visible = false;
            AmberPicture.Visible = false;
            QueueStop(device, DumpToFileCheckbox.Checked);
        }

        private void QueueStop(LibPcapLiveDevice selectedDevice, bool saved)
        {
            if (selectedDevice == null || System.Threading.Interlocked.Exchange(ref stopQueued, 1) != 0) return;
            System.Threading.ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    selectedDevice.StopCapture();
                    selectedDevice.OnPacketArrival -= OnPacketArrival;
                    selectedDevice.Close();
                    lock (packets)
                    {
                        if (writer != null)
                        {
                            writer.Close();
                            writer = null;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Logging.Info("Failed to stop packet capture.", exception);
                }
                finally
                {
                    try { selectedDevice.OnPacketArrival -= OnPacketArrival; selectedDevice.Close(); }
                    catch (Exception exception) { Logging.Info("Unable to close packet capture device.", exception); }
                    lock (packets)
                    {
                        if (writer != null)
                        {
                            try { writer.Close(); }
                            catch (Exception exception) { Logging.Info("Unable to close packet capture file.", exception); }
                            writer = null;
                        }
                    }
                    captureActive = false;
                    captureStarting = false;
                    if (!closing && !IsDisposed && IsHandleCreated)
                        BeginInvoke(new MethodInvoker(() =>
                        {
                            CaptureStopped();
                            if (saved && dumpFile != null)
                                ExternalLinks.OpenFileInNotepad(dumpFile);
                        }));
                }
            });
        }

        private void CaptureStopped()
        {
            captureStarting = false;
            captureActive = false;
            updateTimer.Stop();
            UpdateUI();
            CaptureButton.Enabled = true;
            StopCaptureButton.Enabled = false;
            RedPicture.Visible = false;
            GreenPicture.Visible = false;
            AmberPicture.Visible = true;
            DumpToFileCheckbox.Enabled = true;
            promiscuousCheckbox.Enabled = true;
            comboBox1.Enabled = true;
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = listBox1.SelectedIndex;
            lock (packets)
            {
                if (index < 0 || index >= packets.Count)
                    return;
                CapturedPacket packet = packets[index];
                hexBox1.ByteProvider = new Be.Windows.Forms.DynamicByteProvider(packet.Data);
                textBox1.Text = Encoding.Default.GetString(packet.Data);
                treeView1.Nodes.Clear();
                TreeNode header = treeView1.Nodes.Add("Capture");
                header.Nodes.Add("Length: " + packet.Data.Length);
                header.Nodes.Add("Date: " + packet.Timestamp);
                header.Nodes.Add("Link layer: " + packet.LinkLayer);
                treeView1.ExpandAll();
            }
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
}
