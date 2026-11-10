using System;
using System.Drawing;
using System.Windows.Forms;
using Terminals.Data;

namespace Terminals.Forms
{
    /// <summary>
    /// Explicit, opt-in migration of favorites whose protocol is retired HTTP.
    /// Closing the dialog without confirming changes nothing.
    /// </summary>
    internal sealed class LegacyHttpFavoritesDialog : Form
    {
        private readonly RadioButton remove;
        private readonly RadioButton convert;
        private readonly Button apply;

        internal LegacyHttpMigrationChoice SelectedChoice
        {
            get
            {
                if (remove.Checked)
                    return LegacyHttpMigrationChoice.Remove;
                if (convert.Checked)
                    return LegacyHttpMigrationChoice.ConvertToHttps;
                return LegacyHttpMigrationChoice.Cancel;
            }
        }

        internal LegacyHttpFavoritesDialog(int count)
        {
            Text = "Legacy HTTP favorites";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(580, 270);

            var intro = new Label
            {
                Location = new Point(18, 16),
                Size = new Size(540, 65),
                Text = count + " legacy HTTP favorite(s) were found. HTTP connections are no longer supported. " +
                       "Choose what to do with them. Nothing will change if you cancel."
            };
            remove = new RadioButton
            {
                Location = new Point(20, 90),
                Size = new Size(530, 40),
                Text = "Remove HTTP favorites and clean up the empty HTTP group"
            };
            convert = new RadioButton
            {
                Location = new Point(20, 135),
                Size = new Size(530, 45),
                Text = "Convert favorites to HTTPS and append them to the HTTPS group"
            };
            var notice = new Label
            {
                Location = new Point(38, 185),
                Size = new Size(510, 35),
                Text = "Conversion does not verify HTTPS availability. Default port 80 becomes 443; custom ports remain unchanged."
            };

            apply = new Button
            {
                Location = new Point(386, 232),
                Size = new Size(80, 27),
                Text = "Apply",
                DialogResult = DialogResult.OK,
                Enabled = false
            };
            var cancel = new Button
            {
                Location = new Point(474, 232),
                Size = new Size(80, 27),
                Text = "Cancel",
                DialogResult = DialogResult.Cancel
            };
            remove.CheckedChanged += (sender, e) => apply.Enabled = remove.Checked || convert.Checked;
            convert.CheckedChanged += (sender, e) => apply.Enabled = remove.Checked || convert.Checked;

            Controls.Add(intro);
            Controls.Add(remove);
            Controls.Add(convert);
            Controls.Add(notice);
            Controls.Add(apply);
            Controls.Add(cancel);
            AcceptButton = apply;
            CancelButton = cancel;
        }
    }
}
