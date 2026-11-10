using System;
using System.Text;
using System.Windows.Forms;
using Terminals.Common.Connections;
using Terminals.Common.Converters;
using Terminals.Data;

namespace Terminals.Connections
{
    internal class HTTPConnection : Connection
    {
        private readonly MiniBrowser browser = new MiniBrowser();
        public override bool Connected
        {
            get { return true; }
        }

        internal HTTPConnection()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Controls.Add(this.browser);
            this.browser.Dock = DockStyle.Fill;
            this.browser.Parent = this;
            this.ResumeLayout(false);
        }

        public override bool Connect()
        {
            try
            {
                this.Dock = DockStyle.Fill;
                if (Favorite.Protocol != KnownConnectionConstants.HTTPS)
                    throw new InvalidOperationException("HTTP connection targets are retired.");
                Uri target = UrlConverter.TryParseHttpsUrl(UrlConverter.ExtractAbsoluteUrl(Favorite));
                if (target == null)
                    throw new InvalidOperationException("Only HTTPS web targets are supported.");
                string url = target.AbsoluteUri;
                IGuardedSecurity security = this.ResolveFavoriteCredentials();

                if (!String.IsNullOrEmpty(security.UserName) && !String.IsNullOrEmpty(security.Password))
                {
                    string securityValues = string.Format("{0}: {1}", security.UserName, security.Password);
                    string securityHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes(securityValues));
                    string additionalHeaders = string.Format("Authorization: Basic {0}{1}", securityHeader, Environment.NewLine);
                    this.browser.Navigate(url, additionalHeaders);
                }
                else
                {
                    this.browser.Navigate(url);
                }

                this.BringToFront();
                this.browser.BringToFront();
            }
            catch (Exception exc)
            {
                Logging.Error("Connecting to HTTPS", exc);
                LastError = exc.Message;
                return false;
            }
            return true;
        }
    }
}