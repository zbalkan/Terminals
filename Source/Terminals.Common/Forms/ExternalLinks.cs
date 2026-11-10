using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace Terminals.Common.Forms
{
    public class ExternalLinks
    {
        public static void OpenPath(string uri, string arguments = "")
        {
            var context = SynchronizationContext.Current;
            ExternalProcessLauncher.StartDetached(
                new ProcessStartInfo(uri, arguments) { UseShellExecute = true },
                ex =>
                {
                    string message = string.Format("Unable to open path:\r\n'{0}'\r\n{1}", uri, ex.Message);
                    MessageBox.Show(message, "Terminals", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }, context);
        }
    }
}
