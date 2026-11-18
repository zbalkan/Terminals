using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetTools;

namespace Terminals.Network
{
    internal partial class WakeOnLan : UserControl
    {
        private bool sending;
        private int generation;

        public WakeOnLan()
        {
            InitializeComponent();
            Disposed += (sender, args) => ++generation;
        }

        private async void SendButton_Click(object sender, EventArgs e)
        {
            if (sending || IsDisposed || Disposing) return;
            string mac = MACTextbox.Text.Trim();
            if (mac.Length == 0)
            {
                ResultsLabel.Text = "Enter a MAC address.";
                return;
            }

            sending = true;
            SendButton.Enabled = false;
            int version = ++generation;
            try
            {
                // Retain the existing MagicPacket format and broadcast behavior.
                var result = await Task.Run(() =>
                {
                    var packet = new MagicPacket(mac);
                    int sent = packet.WakeUp();
                    return new { Sent = sent, Address = packet.macAddress };
                });
                if (IsDisposed || Disposing || version != generation) return;
                ResultsLabel.Text = string.Format("{0} bytes sent to {1}", result.Sent, result.Address);
            }
            catch (Exception exception)
            {
                if (!IsDisposed && !Disposing && version == generation)
                {
                    Logging.Info("Error sending Magic Packet", exception);
                    ResultsLabel.Text = "Unable to send the Magic Packet: " + exception.Message;
                }
            }
            finally
            {
                sending = false;
                if (!IsDisposed && !Disposing && version == generation)
                    SendButton.Enabled = true;
            }
        }

        private void MACTextbox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                if (!sending) SendButton.PerformClick();
            }
        }
    }
}
