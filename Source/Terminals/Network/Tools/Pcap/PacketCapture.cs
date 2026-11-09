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
        private MethodInvoker updater;

        private const string DumpFile = @"c:\Terminals.dump";

        public PacketCapture()
        {
            InitializeComponent();
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
                updater = UpdateUI;
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
            if (device == null)
            {
                MessageBox.Show("Select a packet capture device.");
                return;
            }

            var selectedDevice = device;
            bool promiscuous = promiscuousCheckbox.Checked;
            bool saveToFile = DumpToFileCheckbox.Checked;
            string filter = FilterTextBox.Text;

            DumpToFileCheckbox.Enabled = false;
            promiscuousCheckbox.Enabled = false;
            comboBox1.Enabled = false;
            CaptureButton.Enabled = false;
            // Enable stop only after the worker has successfully opened the device.
            StopCaptureButton.Enabled = false;
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
                        writer = new CaptureFileWriterDevice(DumpFile);
                        writer.Open(new DeviceConfiguration());
                    }
                    selectedDevice.OnPacketArrival += OnPacketArrival;
                    selectedDevice.StartCapture();
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke(new MethodInvoker(() => StopCaptureButton.Enabled = true));
                }
                catch (Exception exception)
                {
                    Logging.Info("Failed to start packet capture.", exception);
                    try { selectedDevice.Close(); } catch (Exception) { }
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
                packets.Add(entry);
                pendingPackets.Add(entry);
                if (writer != null)
                    writer.Write(raw);
            }
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(updater);
        }

        private void UpdateUI()
        {
            lock (packets)
            {
                foreach (CapturedPacket packet in pendingPackets)
                    listBox1.Items.Add(packet);
                pendingPackets.Clear();
            }
        }

        private void StopCaptureButton_Click(object sender, EventArgs e)
        {
            CaptureButton.Enabled = false;
            StopCaptureButton.Enabled = false;
            RedPicture.Visible = true;
            GreenPicture.Visible = false;
            AmberPicture.Visible = false;
            var selectedDevice = device;
            bool saved = DumpToFileCheckbox.Checked;
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
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke(new MethodInvoker(() =>
                        {
                            CaptureStopped();
                            if (saved)
                                ExternalLinks.OpenFileInNotepad(DumpFile);
                        }));
                }
            });
        }

        private void CaptureStopped()
        {
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
