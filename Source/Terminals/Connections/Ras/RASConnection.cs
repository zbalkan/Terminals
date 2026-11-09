using System;
using System.Windows.Forms;
using Terminals.Connections.RasNative;

namespace Terminals.Connections
{
    internal class RASConnection : Connection
    {
        private bool wasConnected;
        internal RasConnectionClient ras { get; private set; }

        public override bool Connected
        {
            get { return ras != null && ras.Connected; }
        }

        public override bool Connect()
        {
            try
            {
                ras = new RasConnectionClient { EntryName = Favorite.ServerName };
                var properties = new RASProperties { RASConnection = this, Dock = DockStyle.Fill };
                Dock = DockStyle.Fill;
                Controls.Add(properties);
                properties.BringToFront();
                BringToFront();
                properties.Parent = Parent;

                var credentials = ResolveFavoriteCredentials();
                uint error;
                if (!string.IsNullOrEmpty(credentials.UserName) && !string.IsNullOrEmpty(credentials.Password))
                {
                    Log("Using Terminals credentials, dialing...");
                    error = ras.Dial(credentials.UserName, credentials.Password, credentials.Domain);
                }
                else
                {
                    Log("Terminals has no credentials; showing Windows RAS dial dialog...");
                    error = ras.DialDialog(FindForm() == null ? IntPtr.Zero : FindForm().Handle);
                }

                if (error != 0)
                {
                    LastError = "Windows RAS dial failed with error " + error;
                    Log(LastError);
                    ras.Dispose();
                    ras = null;
                    return false;
                }

                wasConnected = true;
                Log("RAS connection established");
                return true;
            }
            catch (Exception exc)
            {
                if (ras != null)
                {
                    ras.Dispose();
                    ras = null;
                }
                LastError = exc.Message;
                Logging.Fatal("Connecting to RAS", exc);
                return false;
            }
        }

        // Called by the existing RAS properties UI timer, on the WinForms thread.
        internal void NotifyIfDisconnected()
        {
            if (wasConnected && !Connected)
            {
                wasConnected = false;
                Log("RAS connection disconnected");
                FireDisconnected();
            }
        }

        public void Disconnect()
        {
            if (ras == null)
                return;
            Log("RAS hang-up result: " + ras.HangUp());
            NotifyIfDisconnected();
            ras.Dispose();
            ras = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && ras != null)
            {
                ras.Dispose();
                ras = null;
            }
            base.Dispose(disposing);
        }
    }
}
