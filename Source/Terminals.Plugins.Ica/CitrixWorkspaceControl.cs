using System;
using System.Drawing;
using System.Windows.Forms;
using Terminals.Data;

namespace Terminals.Forms.EditFavorite
{
    internal sealed class CitrixWorkspaceControl : UserControl, IProtocolOptionsControl
    {
        private readonly ComboBox mode;
        private readonly TextBox resource;

        internal CitrixWorkspaceControl()
        {
            mode = new ComboBox
            {
                Location = new Point(140, 18), Width = 260,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            mode.Items.AddRange(new object[] { "Workspace / Cloud (SSO)", "StoreFront / on-premises" });
            mode.SelectedIndex = 0;
            resource = new TextBox { Location = new Point(140, 53), Width = 260 };
            Controls.Add(new Label { Text = "Launch mode:", Location = new Point(12, 22), Width = 120 });
            Controls.Add(mode);
            Controls.Add(new Label { Text = "Published resource:", Location = new Point(12, 57), Width = 125 });
            Controls.Add(resource);
            Controls.Add(new Label
            {
                Text = "Set the HTTPS store/discovery URL on the General tab. Citrix Workspace handles authentication.",
                Location = new Point(12, 95), Size = new Size(400, 64)
            });
        }

        public void LoadFrom(IFavorite favorite)
        {
            var options = favorite.ProtocolProperties as CitrixWorkspaceOptions;
            if (options == null) return;
            mode.SelectedIndex = options.LaunchMode == "StoreFront" ? 1 : 0;
            resource.Text = options.ResourceName ?? string.Empty;
        }

        public void SaveTo(IFavorite favorite)
        {
            var options = favorite.ProtocolProperties as CitrixWorkspaceOptions;
            if (options == null) return;
            if (string.IsNullOrWhiteSpace(resource.Text))
                throw new ArgumentException("A published Citrix resource name is required.");
            options.LaunchMode = mode.SelectedIndex == 1 ? "StoreFront" : "Cloud";
            options.ResourceName = resource.Text.Trim();
        }
    }
}
