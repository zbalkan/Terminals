using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Terminals.Common.Connections;
using Terminals.Connections.RasNative;

namespace Terminals.Forms.EditFavorite
{
    // The RAS protocol remains disabled in ConnectionManager; retain the editor for existing favorites.
    internal partial class RasControl : UserControl
    {
        private readonly Dictionary<string, RasPhoneBookEntry> dialupList =
            new Dictionary<string, RasPhoneBookEntry>(StringComparer.OrdinalIgnoreCase);

        internal List<string> ConnectionNames { get; private set; }

        internal RasControl()
        {
            InitializeComponent();
            ConnectionNames = new List<string>();
        }

        internal void OnServerNameChanged(string protocolName, string serverName)
        {
            if (protocolName == KnownConnectionConstants.RAS)
                FillRasControls(serverName);
        }

        private void FillRasControls(string serverName)
        {
            LoadDialupConnections();
            RASDetailsListBox.Items.Clear();
            RasPhoneBookEntry entry;
            if (serverName != null && dialupList.TryGetValue(serverName, out entry))
            {
                RASDetailsListBox.Items.Add("Connection: " + entry.Name);
                RASDetailsListBox.Items.Add("Local phone number: " + entry.PhoneNumber);
            }
        }

        public void SetControls()
        {
            LoadDialupConnections();
            RASDetailsListBox.Items.Clear();
        }

        private void LoadDialupConnections()
        {
            dialupList.Clear();
            ConnectionNames.Clear();
            foreach (var entry in RasConnectionClient.GetEntries())
            {
                if (dialupList.ContainsKey(entry.Name))
                    continue;
                dialupList.Add(entry.Name, entry);
                ConnectionNames.Add(entry.Name);
            }
        }
    }
}
