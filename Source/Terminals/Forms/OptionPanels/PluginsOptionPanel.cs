using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Terminals.Common.Configuration;
using Terminals.Common.Connections;
using Terminals.Connections;
using Terminals.Properties;
using Settings = Terminals.Configuration.Settings;

namespace Terminals.Forms.OptionPanels
{
    public partial class PluginsOptionPanel : UserControl, IOptionPanel
    {
        private readonly PluginsSelection pluginsSelection;
        private readonly FlowLayoutPanel pluginGroups;
        private readonly CheckedListBox otherPlugins;
        private readonly CheckBox enablePutty;
        private readonly ComboBox preferredSshClient;
        private readonly TextBox puttyPath;
        private readonly TextBox pageantPath;
        private readonly Panel puttyControls;
        private SelectedPlugin puttyPlugin;
        private SelectedPlugin citrixPlugin;
        private readonly CheckBox enableCitrix;
        private readonly Panel citrixControls;
        private readonly TextBox selfServicePath;
        private readonly TextBox storebrowsePath;
        private bool loading;

        public PluginsOptionPanel()
        {
            InitializeComponent();
            pluginsSelection = new PluginsSelection(Settings.Instance, new PluginsLoader(Settings.Instance));

            // Keep scrolling within the Plugins page, not in the Options dialog.
            pluginGroups = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8)
            };
            Controls.Clear();
            Controls.Add(pluginGroups);

            var sshGroup = new GroupBox { Text = "SSH", Height = 78, Margin = new Padding(3, 3, 3, 10) };
            sshGroup.Controls.Add(new Label { Text = "SSH client", Location = new Point(12, 29), Width = 82 });
            preferredSshClient = new ComboBox
            {
                Location = new Point(100, 25), Width = 240,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            preferredSshClient.Items.Add("PuTTY");
            if (SshClientPreference.FindWindowsOpenSsh() != null)
                preferredSshClient.Items.Add("Windows OpenSSH");
            sshGroup.Controls.Add(preferredSshClient);
            pluginGroups.Controls.Add(sshGroup);

            var puttyGroup = new GroupBox { Text = "PuTTY", Height = 113, Margin = new Padding(3, 3, 3, 10) };
            enablePutty = new CheckBox
            {
                Text = "Enable PuTTY", AutoSize = true,
                Location = new Point(12, 21)
            };
            puttyControls = new Panel { Location = new Point(12, 45), Height = 60 };
            puttyPath = AddExecutableRow(puttyControls, "PuTTY", "putty.exe", 0);
            pageantPath = AddExecutableRow(puttyControls, "Pageant", "pageant.exe", 30);
            enablePutty.CheckedChanged += (sender, args) => puttyControls.Enabled = enablePutty.Checked;
            puttyGroup.Controls.Add(enablePutty);
            puttyGroup.Controls.Add(puttyControls);
            pluginGroups.Controls.Add(puttyGroup);

            var citrixGroup = new GroupBox { Text = "Citrix Workspace", Height = 113, Margin = new Padding(3, 3, 3, 10) };
            enableCitrix = new CheckBox { Text = "Enable Citrix Workspace", AutoSize = true, Location = new Point(12, 21) };
            citrixControls = new Panel { Location = new Point(12, 45), Height = 60 };
            selfServicePath = AddExecutableRow(citrixControls, "SelfSvc", "SelfService.exe", 0);
            storebrowsePath = AddExecutableRow(citrixControls, "Store", "storebrowse.exe", 30);
            enableCitrix.CheckedChanged += (sender, args) => citrixControls.Enabled = enableCitrix.Checked;
            citrixGroup.Controls.Add(enableCitrix);
            citrixGroup.Controls.Add(citrixControls);
            pluginGroups.Controls.Add(citrixGroup);

            var otherGroup = new GroupBox { Text = "Other protocol plugins", Height = 164, Margin = new Padding(3) };
            otherPlugins = new CheckedListBox
            {
                CheckOnClick = true,
                Location = new Point(12, 22),
                IntegralHeight = false,
                Height = 130,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            otherGroup.Controls.Add(otherPlugins);
            pluginGroups.Controls.Add(otherGroup);
            pluginGroups.Resize += (sender, args) => ResizeGroups();
            ResizeGroups();
        }

        private void ResizeGroups()
        {
            int width = Math.Max(300, pluginGroups.ClientSize.Width - pluginGroups.Padding.Horizontal - 12);
            foreach (Control group in pluginGroups.Controls)
                group.Width = width;

            puttyControls.Width = width - 24;
            foreach (Control control in puttyControls.Controls)
            {
                if (control is TextBox)
                    control.Width = Math.Max(80, puttyControls.Width - 165);
                else if (control is Button)
                    control.Left = puttyControls.Width - 76;
            }
            citrixControls.Width = width - 24;
            foreach (Control control in citrixControls.Controls)
            {
                if (control is TextBox)
                    control.Width = Math.Max(80, citrixControls.Width - 165);
                else if (control is Button)
                    control.Left = citrixControls.Width - 76;
            }
            otherPlugins.Width = width - 24;
            preferredSshClient.Width = Math.Max(150, width - 124);
        }

        private static TextBox AddExecutableRow(Panel parent, string caption, string filename, int y)
        {
            var label = new Label { Text = caption, Location = new Point(0, y + 4), Width = 63 };
            var path = new TextBox { Location = new Point(65, y), Width = 200 };
            var browse = new Button { Text = "Browse", Location = new Point(270, y - 1), Size = new Size(74, 23) };
            browse.Click += (sender, args) =>
            {
                using (var dialog = new OpenFileDialog
                {
                    Title = "Locate " + filename,
                    Filter = "Executable files (*.exe)|*.exe",
                    FileName = filename,
                    CheckFileExists = true
                })
                {
                    if (dialog.ShowDialog(parent) == DialogResult.OK)
                        path.Text = dialog.FileName;
                }
            };
            parent.Controls.Add(label);
            parent.Controls.Add(path);
            parent.Controls.Add(browse);
            return path;
        }

        public void LoadSettings()
        {
            loading = true;
            try
            {
                puttyPlugin = null;
                citrixPlugin = null;
                otherPlugins.Items.Clear();
                foreach (SelectedPlugin plugin in pluginsSelection.LoadPlugins())
                {
                    if (Path.GetFileName(plugin.FullPath).Equals("Terminals.Plugins.Putty.dll",
                        StringComparison.OrdinalIgnoreCase))
                        puttyPlugin = plugin;
                    else if (Path.GetFileName(plugin.FullPath).Equals("Terminals.Plugins.Ica.dll",
                        StringComparison.OrdinalIgnoreCase))
                        citrixPlugin = plugin;
                    else
                        otherPlugins.Items.Add(plugin, plugin.Enabled);
                }

                var chosen = SshClientPreference.Parse(Settings.Instance.PreferredSshClient);
                if (chosen == SshClientKind.WindowsOpenSSH)
                {
                    // Keep an unavailable saved preference visible; never silently
                    // rewrite an existing user's chosen SSH backend to PuTTY.
                    if (!preferredSshClient.Items.Contains("Windows OpenSSH"))
                        preferredSshClient.Items.Add("Windows OpenSSH (not installed)");
                    preferredSshClient.SelectedIndex = preferredSshClient.Items.IndexOf(
                        SshClientPreference.FindWindowsOpenSsh() == null
                            ? "Windows OpenSSH (not installed)" : "Windows OpenSSH");
                }
                else
                    preferredSshClient.SelectedItem = "PuTTY";

                enablePutty.Enabled = puttyPlugin != null;
                enablePutty.Checked = puttyPlugin != null && puttyPlugin.Enabled;
                puttyPath.Text = DisplayPath("putty.exe", Settings.Instance.PuttyExecutablePath);
                pageantPath.Text = DisplayPath("pageant.exe", Settings.Instance.PageantExecutablePath);
                puttyControls.Enabled = enablePutty.Checked;
                enableCitrix.Enabled = citrixPlugin != null;
                enableCitrix.Checked = citrixPlugin != null && citrixPlugin.Enabled;
                selfServicePath.Text = DisplayPath("SelfService.exe", Settings.Instance.CitrixSelfServiceExecutablePath);
                storebrowsePath.Text = DisplayPath("storebrowse.exe", Settings.Instance.CitrixStorebrowseExecutablePath);
                citrixControls.Enabled = enableCitrix.Checked;
            }
            finally { loading = false; }
        }

        private static string DisplayPath(string filename, string configured)
        {
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;
            return ExternalExecutablePaths.Find(filename, null) ?? string.Empty;
        }

        public void SaveSettings()
        {
            var validation = new CancelEventArgs();
            OnValidating(validation);
            if (validation.Cancel)
                throw new InvalidOperationException("Enable at least one plugin and correct any invalid executable paths.");

            var choice = (preferredSshClient.SelectedItem as string ?? "").StartsWith("Windows OpenSSH", StringComparison.Ordinal)
                ? SshClientKind.WindowsOpenSSH : SshClientKind.PuTTY;
            Settings.Instance.PreferredSshClient = choice.ToString();
            SshClientPreference.Selected = choice;
            pluginsSelection.SaveSelected(GetPluginsFromUI());
            SaveExecutablePath(puttyPath, "putty.exe", value => Settings.Instance.PuttyExecutablePath = value);
            SaveExecutablePath(pageantPath, "pageant.exe", value => Settings.Instance.PageantExecutablePath = value);
            ExternalExecutablePaths.Putty = Settings.Instance.PuttyExecutablePath;
            ExternalExecutablePaths.Pageant = Settings.Instance.PageantExecutablePath;
            SaveExecutablePath(selfServicePath, "SelfService.exe", value => Settings.Instance.CitrixSelfServiceExecutablePath = value);
            SaveExecutablePath(storebrowsePath, "storebrowse.exe", value => Settings.Instance.CitrixStorebrowseExecutablePath = value);
            ExternalExecutablePaths.CitrixSelfService = Settings.Instance.CitrixSelfServiceExecutablePath;
            ExternalExecutablePaths.CitrixStorebrowse = Settings.Instance.CitrixStorebrowseExecutablePath;
        }

        private static void SaveExecutablePath(TextBox textbox, string executable, Action<string> save)
        {
            string value = textbox.Text.Trim();
            string detected = ExternalExecutablePaths.Find(executable, null);
            // A discovered value is displayed, but is not a persistent manual override.
            save(string.Equals(value, detected ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                ? string.Empty : value);
        }

        protected override void OnValidating(CancelEventArgs e)
        {
            base.OnValidating(e);
            if (loading) return;

            e.Cancel = !GetPluginsFromUI().Any(p => p.Enabled) &&
                !(puttyPlugin != null && (preferredSshClient.SelectedItem as string ?? "")
                  .StartsWith("Windows OpenSSH", StringComparison.Ordinal));
            errorProvider.SetError(otherPlugins, e.Cancel ? Resources.PluginSelectionErrorMessage : string.Empty);
            if (e.Cancel) return;

            foreach (var row in new[] {
                new { Name = "putty.exe", Field = puttyPath },
                new { Name = "pageant.exe", Field = pageantPath },
                new { Name = "SelfService.exe", Field = selfServicePath },
                new { Name = "storebrowse.exe", Field = storebrowsePath }
            })
            {
                errorProvider.SetError(row.Field, string.Empty);
                bool isCitrixRow = row.Name == "SelfService.exe" || row.Name == "storebrowse.exe";
                if (!(isCitrixRow ? enableCitrix.Checked : enablePutty.Checked) ||
                    string.IsNullOrWhiteSpace(row.Field.Text))
                    continue;
                try { ExternalExecutablePaths.Find(row.Name, row.Field.Text); }
                catch (Exception ex) when (ex is FileNotFoundException || ex is ArgumentException ||
                                           ex is NotSupportedException || ex is PathTooLongException)
                {
                    e.Cancel = true;
                    errorProvider.SetError(row.Field, ex.Message);
                    return;
                }
            }
        }

        private List<SelectedPlugin> GetPluginsFromUI()
        {
            var selected = new List<SelectedPlugin>();
            if (puttyPlugin != null)
            {
                puttyPlugin.Enabled = enablePutty.Checked;
                selected.Add(puttyPlugin);
            }
            if (citrixPlugin != null)
            {
                citrixPlugin.Enabled = enableCitrix.Checked;
                selected.Add(citrixPlugin);
            }
            for (int i = 0; i < otherPlugins.Items.Count; i++)
            {
                var plugin = (SelectedPlugin)otherPlugins.Items[i];
                plugin.Enabled = otherPlugins.GetItemChecked(i);
                selected.Add(plugin);
            }
            return selected;
        }
    }
}
