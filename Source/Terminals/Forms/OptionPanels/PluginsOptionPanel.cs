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
        private SelectedPlugin puttyPlugin;
        private SelectedPlugin citrixPlugin;
        private SelectedPlugin vncPlugin;
        private readonly Dictionary<string, string> vncViewerPaths = new Dictionary<string, string>();
        private string displayedVncViewer;
        private bool loading;

        public PluginsOptionPanel()
        {
            InitializeComponent();
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;
            pluginsSelection = new PluginsSelection(Settings.Instance, new PluginsLoader(Settings.Instance));
            if (SshClientPreference.FindWindowsOpenSsh() != null)
                preferredSshClient.Items.Add("Windows OpenSSH");
            ResizeGroups();
        }
        private void EnablePutty_CheckedChanged(object sender, EventArgs e) { puttyControls.Enabled = enablePutty.Checked; }
        private void EnableCitrix_CheckedChanged(object sender, EventArgs e) { citrixControls.Enabled = enableCitrix.Checked; }
        private void EnableVnc_CheckedChanged(object sender, EventArgs e)
        {
            preferredVncViewer.Enabled = enableVnc.Checked;
            UpdateVncPathControls();
        }
        private void PreferredVncViewer_SelectedIndexChanged(object sender, EventArgs e) { UpdateVncPathControls(); }
        private void VncViewerPath_TextChanged(object sender, EventArgs e)
        {
            if (!loading && displayedVncViewer != null)
                vncViewerPaths[displayedVncViewer] = vncViewerPath.Text;
        }
        private void PluginGroups_Resize(object sender, EventArgs e) { ResizeGroups(); }
        private void BrowsePutty_Click(object sender, EventArgs e) { BrowseExecutable(puttyPath, puttyControls, "putty.exe"); }
        private void BrowsePageant_Click(object sender, EventArgs e) { BrowseExecutable(pageantPath, puttyControls, "pageant.exe"); }
        private void BrowsePuttygen_Click(object sender, EventArgs e) { BrowseExecutable(puttygenPath, puttyControls, "puttygen.exe"); }
        private void BrowseSelfService_Click(object sender, EventArgs e) { BrowseExecutable(selfServicePath, citrixControls, "SelfService.exe"); }
        private void BrowseStorebrowse_Click(object sender, EventArgs e) { BrowseExecutable(storebrowsePath, citrixControls, "storebrowse.exe"); }
        private void BrowseVnc_Click(object sender, EventArgs e)
        {
            string vendor = preferredVncViewer.SelectedItem as string;
            if (vendor != null)
                BrowseExecutable(vncViewerPath, vncControls, ExternalVncViewers.ExecutableName(vendor), "Locate " + vendor + " viewer");
        }
        private static void BrowseExecutable(TextBox path, Control owner, string filename, string title = null)
        {
            using (var dialog = new OpenFileDialog
            {
                Title = title ?? "Locate " + filename,
                Filter = "Executable files (*.exe)|*.exe",
                FileName = filename,
                CheckFileExists = true
            })
            {
                if (dialog.ShowDialog(owner) == DialogResult.OK)
                    path.Text = dialog.FileName;
            }
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
            vncControls.Width = width - 24;
            foreach (Control control in vncControls.Controls)
            {
                if (control is TextBox)
                    control.Width = Math.Max(80, vncControls.Width - 165);
                else if (control is Button)
                    control.Left = vncControls.Width - 76;
            }
            preferredVncViewer.Width = Math.Max(150, width - 128);
            otherPlugins.Width = width - 24;
            preferredSshClient.Width = Math.Max(150, width - 124);
        }

        private void UpdateVncPathControls()
        {
            string selected = preferredVncViewer.SelectedItem as string;
            vncControls.Enabled = enableVnc.Checked;
            if (selected == displayedVncViewer) return;
            if (!loading && displayedVncViewer != null)
                vncViewerPaths[displayedVncViewer] = vncViewerPath.Text;
            displayedVncViewer = selected;
            string path;
            vncViewerPath.Text = selected != null && vncViewerPaths.TryGetValue(selected, out path)
                ? path : string.Empty;
        }

        public void LoadSettings()
        {
            loading = true;
            try
            {
                puttyPlugin = null;
                citrixPlugin = null;
                vncPlugin = null;
                otherPlugins.Items.Clear();
                foreach (SelectedPlugin plugin in pluginsSelection.LoadPlugins())
                {
                    if (Path.GetFileName(plugin.FullPath).Equals("Terminals.Plugins.Putty.dll",
                        StringComparison.OrdinalIgnoreCase))
                        puttyPlugin = plugin;
                    else if (Path.GetFileName(plugin.FullPath).Equals("Terminals.Plugins.Vnc.dll",
                        StringComparison.OrdinalIgnoreCase))
                        vncPlugin = plugin;
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
                puttygenPath.Text = DisplayPath("puttygen.exe", Settings.Instance.PuttygenExecutablePath);
                puttyControls.Enabled = enablePutty.Checked;
                enableCitrix.Enabled = citrixPlugin != null;
                enableCitrix.Checked = citrixPlugin != null && citrixPlugin.Enabled;
                selfServicePath.Text = DisplayPath("SelfService.exe", Settings.Instance.CitrixSelfServiceExecutablePath);
                storebrowsePath.Text = DisplayPath("storebrowse.exe", Settings.Instance.CitrixStorebrowseExecutablePath);
                citrixControls.Enabled = enableCitrix.Checked;
                enableVnc.Enabled = vncPlugin != null;
                enableVnc.Checked = vncPlugin != null && vncPlugin.Enabled;
                preferredVncViewer.SelectedItem = ExternalVncViewers.IsSupported(Settings.Instance.PreferredVncViewer)
                    ? Settings.Instance.PreferredVncViewer : ExternalVncViewers.TightVnc;
                vncViewerPaths.Clear();
                vncViewerPaths[ExternalVncViewers.TightVnc] =
                    DisplayVncPath(ExternalVncViewers.TightVnc, Settings.Instance.VncViewerExecutablePath);
                vncViewerPaths[ExternalVncViewers.UltraVnc] =
                    DisplayVncPath(ExternalVncViewers.UltraVnc, Settings.Instance.UltraVncExecutablePath);
                vncViewerPaths[ExternalVncViewers.TigerVnc] =
                    DisplayVncPath(ExternalVncViewers.TigerVnc, Settings.Instance.TigerVncExecutablePath);
                vncViewerPaths[ExternalVncViewers.RealVnc] =
                    DisplayVncPath(ExternalVncViewers.RealVnc, Settings.Instance.RealVncExecutablePath);
                displayedVncViewer = null;
                preferredVncViewer.Enabled = enableVnc.Checked;
                UpdateVncPathControls();
            }
            finally { loading = false; }
        }

        private static string DisplayVncPath(string vendor, string configured)
        {
            // Preserve an invalid or obsolete override verbatim so it can be repaired
            // in the settings dialog; validation happens on save or launch.
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;
            return ExternalVncViewers.Find(vendor, null) ?? string.Empty;
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
            SaveExecutablePath(puttygenPath, "puttygen.exe", value => Settings.Instance.PuttygenExecutablePath = value);
            ExternalExecutablePaths.Putty = Settings.Instance.PuttyExecutablePath;
            ExternalExecutablePaths.Pageant = Settings.Instance.PageantExecutablePath;
            ExternalExecutablePaths.Puttygen = Settings.Instance.PuttygenExecutablePath;
            SaveExecutablePath(selfServicePath, "SelfService.exe", value => Settings.Instance.CitrixSelfServiceExecutablePath = value);
            SaveExecutablePath(storebrowsePath, "storebrowse.exe", value => Settings.Instance.CitrixStorebrowseExecutablePath = value);
            ExternalExecutablePaths.CitrixSelfService = Settings.Instance.CitrixSelfServiceExecutablePath;
            ExternalExecutablePaths.CitrixStorebrowse = Settings.Instance.CitrixStorebrowseExecutablePath;
            Settings.Instance.PreferredVncViewer = preferredVncViewer.SelectedItem as string ?? ExternalVncViewers.TightVnc;
            if (displayedVncViewer != null)
                vncViewerPaths[displayedVncViewer] = vncViewerPath.Text;
            Settings.Instance.VncViewerExecutablePath = SavedVncPath(ExternalVncViewers.TightVnc);
            Settings.Instance.UltraVncExecutablePath = SavedVncPath(ExternalVncViewers.UltraVnc);
            Settings.Instance.TigerVncExecutablePath = SavedVncPath(ExternalVncViewers.TigerVnc);
            Settings.Instance.RealVncExecutablePath = SavedVncPath(ExternalVncViewers.RealVnc);
            ExternalVncViewers.Selected = Settings.Instance.PreferredVncViewer;
            ExternalVncViewers.UltraVncPath = Settings.Instance.UltraVncExecutablePath;
            ExternalVncViewers.TigerVncPath = Settings.Instance.TigerVncExecutablePath;
            ExternalVncViewers.RealVncPath = Settings.Instance.RealVncExecutablePath;
            ExternalExecutablePaths.VncViewer = Settings.Instance.VncViewerExecutablePath;
        }

        private string SavedVncPath(string vendor)
        {
            string value;
            if (!vncViewerPaths.TryGetValue(vendor, out value))
                return string.Empty;
            value = value.Trim();
            string detected = ExternalVncViewers.Find(vendor, null);
            return string.Equals(value, detected ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                ? string.Empty : value;
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
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || loading) return;

            e.Cancel = !GetPluginsFromUI().Any(p => p.Enabled) &&
                !(puttyPlugin != null && (preferredSshClient.SelectedItem as string ?? "")
                  .StartsWith("Windows OpenSSH", StringComparison.Ordinal));
            errorProvider.SetError(otherPlugins, e.Cancel ? Resources.PluginSelectionErrorMessage : string.Empty);
            if (e.Cancel) return;

            foreach (var row in new[] {
                new { Name = "putty.exe", Field = puttyPath },
                new { Name = "pageant.exe", Field = pageantPath },
                new { Name = "puttygen.exe", Field = puttygenPath },
                new { Name = "SelfService.exe", Field = selfServicePath },
                new { Name = "storebrowse.exe", Field = storebrowsePath },
                new { Name = "vnc-selected", Field = vncViewerPath }
            })
            {
                errorProvider.SetError(row.Field, string.Empty);
                bool isCitrixRow = row.Name == "SelfService.exe" || row.Name == "storebrowse.exe";
                string selectedVnc = preferredVncViewer.SelectedItem as string;
                bool isVnc = row.Name == "vnc-selected";
                string rowVendor = selectedVnc;
                bool rowEnabled = isVnc ? enableVnc.Checked && selectedVnc == rowVendor :
                    (isCitrixRow ? enableCitrix.Checked : enablePutty.Checked);
                if (!rowEnabled ||
                    string.IsNullOrWhiteSpace(row.Field.Text))
                    continue;
                try
                {
                    if (isVnc) ExternalVncViewers.Find(rowVendor, row.Field.Text);
                    else ExternalExecutablePaths.Find(row.Name, row.Field.Text);
                }
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
            if (vncPlugin != null)
            {
                vncPlugin.Enabled = enableVnc.Checked;
                selected.Add(vncPlugin);
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
