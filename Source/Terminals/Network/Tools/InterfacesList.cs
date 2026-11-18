using System;
using System.Collections.Generic;
using System.Data;
using System.Management;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Terminals.Network
{
    internal partial class InterfacesList : UserControl
    {
        private bool loading;
        private int generation;

        public InterfacesList()
        {
            InitializeComponent();
            Disposed += (sender, args) => ++generation;
        }

        private async void InterfacesList_Load(object sender, EventArgs e)
        {
            if (loading || IsDisposed) return;
            loading = true;
            int version = ++generation;
            try
            {
                dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
                dataGridView2.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
                var snapshot = await Task.Run(() => new
                {
                    Interfaces = NetworkInterfaceInventory.GetIPv4Interfaces(),
                    Adapters = CaptureAdapterSnapshot()
                });
                if (IsDisposed || Disposing || version != generation) return;
                dataGridView1.DataSource = snapshot.Interfaces;
                dataGridView2.DataSource = snapshot.Adapters;
            }
            catch (Exception exception)
            {
                if (!IsDisposed && !Disposing && version == generation)
                    Logging.Info("Unable to enumerate local network interfaces.", exception);
            }
            finally
            {
                loading = false;
            }
        }

        internal static string FormatArray(Array values)
        {
            var items = new List<string>();
            foreach (object value in values)
                items.Add(Convert.ToString(value));
            return string.Join(", ", items);
        }

        // Materialize WMI values on the worker, not when DataGridView requests properties.
        private static DataTable CaptureAdapterSnapshot()
        {
            var table = new DataTable("Network adapters");
            using (var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_NetworkAdapterConfiguration"))
            using (ManagementObjectCollection results = searcher.Get())
            {
                foreach (ManagementObject adapter in results)
                {
                    using (adapter)
                    {
                        foreach (PropertyData property in adapter.Properties)
                            if (!table.Columns.Contains(property.Name))
                                table.Columns.Add(property.Name, typeof(string));

                        DataRow row = table.NewRow();
                        foreach (PropertyData property in adapter.Properties)
                        {
                            object value = property.Value;
                            if (value is Array)
                                row[property.Name] = FormatArray((Array)value);
                            else
                                row[property.Name] = value == null ? string.Empty : Convert.ToString(value);
                        }
                        table.Rows.Add(row);
                    }
                }
            }
            return table;
        }
    }
}
