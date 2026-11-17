using System;
using System.Windows.Forms;
using Terminals.Data;

namespace Terminals.Forms.EditFavorite
{
    internal sealed partial class CitrixWorkspaceControl : UserControl, IProtocolOptionsControl
    {
        public CitrixWorkspaceControl()
        {
            InitializeComponent();
            mode.SelectedIndex = 0;
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
