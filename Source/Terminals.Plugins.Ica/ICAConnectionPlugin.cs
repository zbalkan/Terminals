using System;
using System.Drawing;
using System.Windows.Forms;
using Terminals.Common.Connections;
using Terminals.Data;
using Terminals.Forms.EditFavorite;
using Terminals.Integration.Export;
using Terminals.Plugins.Ica;

namespace Terminals.Connections.ICA
{
    internal sealed class LegacyIcaDbAliasPlugin : IConnectionPlugin, IOptionsConverterFactory, IRetiredConnectionPlugin
    {
        // Historical SQL stored procedures truncate "ICA Citrix" to five characters.
        private readonly ICAConnectionPlugin legacy = new ICAConnectionPlugin();
        public int Port { get { return 0; } }
        public string PortName { get { return KnownConnectionConstants.LEGACY_ICA_SQL; } }
        public Connection CreateConnection()
        {
            throw new NotSupportedException("Legacy ICA favorites cannot be connected.");
        }
        public Control[] CreateOptionsControls() { return legacy.CreateOptionsControls(); }
        public Type GetOptionsType() { return typeof(ICAOptions); }
        public ProtocolOptions CreateOptions() { return new ICAOptions(); }
        public Image GetIcon() { return Connection.Terminalsicon; }
        public IOptionsConverter CreatOptionsConverter() { return new IcaOptionsConverter(); }
    }

    internal class ICAConnectionPlugin : IConnectionPlugin, IOptionsExporterFactory, IOptionsConverterFactory, IRetiredConnectionPlugin
    {
        internal const int ICAPort = 1494;

        internal const string ICA_CITRIX = "ICA Citrix";

        public int Port { get { return ICAPort; } }

        public string PortName { get { return ICA_CITRIX; } }

        public Connection CreateConnection()
        {
            throw new NotSupportedException("Legacy ICA Citrix favorites cannot be connected. Create a Citrix Workspace favorite.");
        }

        public Control[] CreateOptionsControls()
        {
            return new Control[] { new CitrixControl() { Name = "Legacy ICA (read-only)", Enabled = false } };
        }

        public Type GetOptionsType()
        {
            return typeof (ICAOptions);
        }

        public ProtocolOptions CreateOptions()
        {
            return new ICAOptions();
        }

        public ITerminalsOptionsExport CreateOptionsExporter()
        {
            return new TerminalsIcaExport();
        }

        public Image GetIcon()
        {
            return  Connection.Terminalsicon;
        }

        public IOptionsConverter CreatOptionsConverter()
        {
            return new IcaOptionsConverter();
        }
    }
}
