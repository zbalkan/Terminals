using System;
using System.Windows.Forms;
using Terminals.Common.Converters;

namespace Terminals.Connections
{
    internal partial class MiniBrowser : UserControl
    {
        private string homeUrl;

        public MiniBrowser()
        {
            InitializeComponent();
            webBrowser1.Navigating += WebBrowserNavigating;
            // Block pop-ups so a target cannot bypass the HTTPS-only navigation policy.
            webBrowser1.NewWindow += (sender, e) => e.Cancel = true;
        }

        private static void WebBrowserNavigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            if (e.Url == null || e.Url.Scheme != Uri.UriSchemeHttps)
                e.Cancel = true;
        }

        private void BackButton_Click(object sender, EventArgs e)
        {
            this.webBrowser1.GoBack();
        }

        private void HomeButton_Click(object sender, EventArgs e)
        {
            this.webBrowser1.Navigate(homeUrl);
        }

        private void ForwardButton_Click(object sender, EventArgs e)
        {
            this.webBrowser1.GoForward();
        }

        internal void Navigate(string url, string additionalHeaders)
        {
            if (UrlConverter.TryParseHttpsUrl(url) == null)
                throw new ArgumentException("Only HTTPS navigation is supported.", "url");
            this.homeUrl = url;
            this.webBrowser1.Navigate(this.homeUrl, null, null, additionalHeaders);
        }

        internal void Navigate(string url)
        {
            if (UrlConverter.TryParseHttpsUrl(url) == null)
                throw new ArgumentException("Only HTTPS navigation is supported.", "url");
            this.homeUrl = url;
            this.webBrowser1.Navigate(this.homeUrl);
        }
    }
}
