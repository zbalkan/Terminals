using System;
using System.Windows.Forms;
using Terminals.Data;

namespace Terminals.Forms.EditFavorite
{
    internal partial class VncControl : UserControl, IProtocolOptionsControl
    {
        private int legacyDisplayNumber;

        public VncControl()
        {
            InitializeComponent();
            this.Controls.Add(new Label
            {
                Text = "TightVNC Viewer opens separately and prompts for authentication. Saved VNC passwords are not forwarded.",
                Size = new System.Drawing.Size(550, 40), Location = new System.Drawing.Point(6, 85)
            });
        }

        public void SaveTo(IFavorite favorite)
        {
            var vncOptions = favorite.ProtocolProperties as VncOptions;
            if (vncOptions == null)
                return;

            // Preserve legacy display numbers when editing favorites, even though
            // the external viewer uses the favorite's explicit port.
            vncOptions.DisplayNumber = this.legacyDisplayNumber;
            vncOptions.AutoScale = this.vncAutoScaleCheckbox.Checked;
            vncOptions.ViewOnly = this.VncViewOnlyCheckbox.Checked;
        }

        public void LoadFrom(IFavorite favorite)
        {
            VncOptions vncOptions = favorite.ProtocolProperties as VncOptions;
            if (vncOptions == null)
                return;
            this.legacyDisplayNumber = vncOptions.DisplayNumber;
            this.vncAutoScaleCheckbox.Checked = vncOptions.AutoScale;
            this.VncViewOnlyCheckbox.Checked = vncOptions.ViewOnly;
        }
    }
}
