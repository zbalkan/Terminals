using System;
using System.Drawing;
using System.Windows.Forms;
using Terminals.Common.Connections;
using Terminals.Data;
using Terminals.Forms.EditFavorite;
using Terminals.Plugins.Ica;

namespace Terminals.Connections.ICA
{
    internal sealed class CitrixWorkspaceConnectionPlugin : IConnectionPlugin, IOptionsConverterFactory
    {
        public int Port { get { return 0; } } // Not a socket connection or scan target.
        public string PortName { get { return KnownConnectionConstants.CITRIX_WORKSPACE; } }
        public Connection CreateConnection() { return new CitrixWorkspaceConnection(); }
        public Control[] CreateOptionsControls()
        {
            return new Control[] { new CitrixWorkspaceControl { Name = "Citrix Workspace" } };
        }
        public Type GetOptionsType() { return typeof(CitrixWorkspaceOptions); }
        public ProtocolOptions CreateOptions() { return new CitrixWorkspaceOptions(); }
        public Image GetIcon() { return Connection.Terminalsicon; }
        public IOptionsConverter CreatOptionsConverter() { return new CitrixWorkspaceOptionsConverter(); }
    }
}
