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
            if (ras != null)
            {
                LastError = "The previous RAS connection must be cleaned up before dialing again.";
                return false;
            }

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
                    throw new NotSupportedException("Managed RAS requires explicit credentials; use Windows to manage dialog-based connections.");
                }

                if (error != 0)
                {
                    LastError = "Windows RAS dial failed with error " + error;
                    Log(LastError);
                    TryReleaseClient();
                    return false;
                }

                wasConnected = true;
                Log("RAS connection established");
                return true;
            }
            catch (Exception exc)
            {
                LastError = exc.Message;
                Logging.Fatal("Connecting to RAS", exc);
                // Retain a client whose failed dial already attempted cleanup.
                // Disconnect/Dispose can retry without issuing a second accepted hang-up.
                if (ras != null && !ras.HasOutstandingHandle)
                    TryReleaseClient();
                return false;
            }
        }

        // Called by the existing RAS properties UI timer, on the WinForms thread.
        internal void NotifyIfDisconnected()
        {
            try
            {
                if (wasConnected && !Connected && (ras == null || !ras.HasOutstandingHandle))
                {
                    // Retire the client before notifying subscribers, which may reconnect.
                    if (!TryReleaseClient())
                        return;
                    wasConnected = false;
                    Log("RAS connection disconnected");
                    FireDisconnected();
                }
            }
            catch (System.ComponentModel.Win32Exception exc)
            {
                LastError = exc.Message;
                Log("RAS status query failed: " + exc.NativeErrorCode);
            }
        }

        public void Disconnect()
        {
            if (ras == null)
                return;
            if (TryReleaseClient() && wasConnected)
            {
                wasConnected = false;
                Log("RAS connection disconnected");
                FireDisconnected();
            }
        }

        private bool TryReleaseClient()
        {
            if (ras == null)
                return true;
            try
            {
                ras.Dispose();
                ras = null;
                return true;
            }
            catch (System.ComponentModel.Win32Exception exc)
            {
                LastError = exc.Message;
                Log(LastError);
                return false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !TryReleaseClient())
                throw new InvalidOperationException(LastError);
            base.Dispose(disposing);
        }
    }
}
