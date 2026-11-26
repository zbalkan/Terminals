using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        private bool loading;
        private bool loaded;
        private string requestedName;

        internal List<string> ConnectionNames { get; private set; }

        internal RasControl()
        {
            InitializeComponent();
            ConnectionNames = new List<string>();
        }

        internal void OnServerNameChanged(string protocolName, string serverName)
        {
            if (protocolName == KnownConnectionConstants.RAS)
            {
                requestedName = serverName;
                FillRasControls(serverName);
                if (!loaded)
                    LoadDialupConnections();
            }
        }

        private void FillRasControls(string serverName)
        {
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

        private async void LoadDialupConnections()
        {
            if (loading || IsDisposed || Disposing)
                return;
            loading = true;
            try
            {
                var entries = await Task.Run(() => RasConnectionClient.GetEntries());
                if (IsDisposed || Disposing)
                    return;
                dialupList.Clear();
                ConnectionNames.Clear();
                foreach (var entry in entries)
                {
                    if (dialupList.ContainsKey(entry.Name))
                        continue;
                    dialupList.Add(entry.Name, entry);
                    ConnectionNames.Add(entry.Name);
                }
                loaded = true;
                FillRasControls(requestedName);
            }
            catch (Exception exc)
            {
                if (!IsDisposed && !Disposing)
                {
                    RASDetailsListBox.Items.Clear();
                    RASDetailsListBox.Items.Add("RAS phonebook unavailable: " + exc.Message);
                }
            }
            finally { loading = false; }
        }
    }
}
