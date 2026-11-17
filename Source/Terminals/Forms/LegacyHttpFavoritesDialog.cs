using System;
using System.Windows.Forms;
using Terminals.Data;

namespace Terminals.Forms
{
    /// <summary>
    /// Explicit, opt-in migration of favorites whose protocol is retired HTTP.
    /// Closing the dialog without confirming changes nothing.
    /// </summary>
    internal sealed partial class LegacyHttpFavoritesDialog : Form
    {
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

        // Required by the WinForms designer. Runtime callers supply the actual count.
        public LegacyHttpFavoritesDialog()
        {
            InitializeComponent();
        }

        internal LegacyHttpFavoritesDialog(int count) : this()
        {
            intro.Text = count + " legacy HTTP favorite(s) were found. HTTP connections are no longer supported. " +
                         "Choose what to do with them. Nothing will change if you cancel.";
        }

        private void MigrationChoiceChanged(object sender, EventArgs e)
        {
            apply.Enabled = remove.Checked || convert.Checked;
        }
    }
}
