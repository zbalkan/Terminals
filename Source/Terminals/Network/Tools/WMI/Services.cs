using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using System.Data;

namespace Terminals.Network.WMI
{
    internal partial class Services : UserControl
    {
        public Services()
        {
            InitializeComponent();
            Disposed += (sender, args) =>
            {
                if (!busy) DisposeCachedObjects();
            };
        }


        private List<ManagementObject> list = new List<ManagementObject>();
        private bool busy;

        private sealed class ServicesResult
        {
            internal readonly DataTable Table = new DataTable();
            internal readonly List<ManagementObject> Objects = new List<ManagementObject>();
        }

        private void SetBusy(bool value)
        {
            busy = value;
            button1.Enabled = !value;
            dataGridView1.Enabled = !value;
            contextMenuStrip1.Enabled = !value;
        }

        private async void LoadServices(string username, string password, string computer)
        {
            if (busy || IsDisposed) return;
            string cacheHost = RemoteWmiAvailability.EffectiveHost(computer, username, password);
            if (!RemoteWmiAvailability.AllowAttempt(cacheHost, this)) return;

            SetBusy(true);
            ServicesResult result = null;
            try
            {
                result = await Task.Run(() => LoadServicesCore(username, password, computer));
                if (IsDisposed) return;
                dataGridView1.DataSource = result.Table;
                List<ManagementObject> oldObjects = list;
                list = result.Objects;
                DisposeObjects(oldObjects);
                result = null;
                RemoteWmiAvailability.Succeeded(cacheHost);
            }
            catch (Exception error)
            {
                RemoteWmiAvailability.Failed(cacheHost, error);
                Terminals.Logging.Info("Windows Services query failed", error);
                if (!IsDisposed)
                    MessageBox.Show(this, "Unable to query services: " + error.Message);
            }
            finally
            {
                if (result != null)
                    DisposeObjects(result.Objects);
                if (IsDisposed)
                    DisposeCachedObjects();
                else
                    SetBusy(false);
            }
        }

        private void DisposeCachedObjects()
        {
            DisposeObjects(list);
            list.Clear();
        }

        private static void DisposeObjects(IEnumerable<ManagementObject> objects)
        {
            foreach (ManagementObject item in objects)
                item.Dispose();
        }

        private static ServicesResult LoadServicesCore(string username, string password, string computer)
        {
            const string qry = "select AcceptPause, AcceptStop, Caption, CheckPoint, CreationClassName, Description, DesktopInteract, DisplayName, ErrorControl, ExitCode, InstallDate, Name, PathName, ProcessId,ServiceSpecificExitCode, ServiceType, Started, StartMode, StartName, State, Status, SystemCreationClassName, SystemName, TagId, WaitHint from win32_service";
            ObjectQuery query = new ObjectQuery(qry);
            ManagementObjectSearcher searcher;
            if (username != "" && password != "" && computer != "" && !computer.StartsWith(@"\\localhost"))
            {
                var options = new ConnectionOptions { Username = username, Password = password };
                if (!computer.StartsWith(@"\\")) computer = @"\\" + computer;
                if (!computer.ToLowerInvariant().EndsWith(@"\root\cimv2")) computer += @"\root\cimv2";
                searcher = new ManagementObjectSearcher(new ManagementScope(computer, options), query);
            }
            else
            {
                searcher = new ManagementObjectSearcher(query);
            }

            var result = new ServicesResult();
            try
            {
                using (searcher)
                using (ManagementObjectCollection collection = searcher.Get())
                {
                    bool needsSchema = true;
                    foreach (ManagementObject service in collection)
                    {
                        result.Objects.Add(service);
                        if (needsSchema)
                        {
                            foreach (PropertyData property in service.Properties)
                                result.Table.Columns.Add(new DataColumn(property.Name, ConvertCimType(property.Type)));
                            needsSchema = false;
                        }

                        object[] values = new object[result.Table.Columns.Count];
                        int column = 0;
                        foreach (PropertyData property in service.Properties)
                            if (column < values.Length)
                                values[column++] = property.Value ?? DBNull.Value;
                        result.Table.Rows.Add(values);
                    }
                }
                return result;
            }
            catch
            {
                DisposeObjects(result.Objects);
                throw;
            }
        }

        private void Services_Load(object sender, EventArgs e)
        {
            LoadServices("", "", "");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            LoadServices(wmiServerCredentials1.Username, wmiServerCredentials1.Password,
                wmiServerCredentials1.SelectedServer);
        }

        private static System.Type ConvertCimType(CimType ctValue)
        {
            System.Type tReturnVal = null;
            switch (ctValue)
            {
                case CimType.Boolean:
                    tReturnVal = typeof(System.Boolean);
                    break;
                case CimType.Char16:
                    tReturnVal = typeof(System.String);
                    break;
                case CimType.DateTime:
                    tReturnVal = typeof(System.DateTime);
                    break;
                case CimType.Object:
                    tReturnVal = typeof(System.Object);
                    break;
                case CimType.Real32:
                    tReturnVal = typeof(System.Decimal);
                    break;
                case CimType.Real64:
                    tReturnVal = typeof(System.Decimal);
                    break;
                case CimType.Reference:
                    tReturnVal = typeof(System.Object);
                    break;
                case CimType.SInt16:
                    tReturnVal = typeof(System.Int16);
                    break;
                case CimType.SInt32:
                    tReturnVal = typeof(System.Int32);
                    break;
                case CimType.SInt8:
                    tReturnVal = typeof(System.Int16);
                    break;
                case CimType.String:
                    tReturnVal = typeof(System.String);
                    break;
                case CimType.UInt16:
                    tReturnVal = typeof(System.UInt16);
                    break;
                case CimType.UInt32:
                    tReturnVal = typeof(System.UInt32);
                    break;
                case CimType.UInt64:
                    tReturnVal = typeof(System.UInt64);
                    break;
                case CimType.UInt8:
                    tReturnVal = typeof(System.UInt16);
                    break;
            }
            return tReturnVal;
        }

        private static ManagementObject FindWMIObject(IEnumerable<ManagementObject> objects, string name)
        {
            foreach (ManagementObject obj in objects)
            {
                PropertyData property = obj.Properties["Name"];
                if (property != null && string.Equals(Convert.ToString(property.Value), name, StringComparison.Ordinal))
                    return obj;
            }
            return null;
        }

        private async void RunServiceCommand(string method)
        {
            if (busy || IsDisposed || dataGridView1.SelectedCells.Count == 0) return;
            var row = dataGridView1.Rows[dataGridView1.SelectedCells[0].RowIndex];
            if (!dataGridView1.Columns.Contains("Name")) return;
            string name = Convert.ToString(row.Cells["Name"].Value);
            if (string.IsNullOrEmpty(name)) return;

            string username = wmiServerCredentials1.Username;
            string password = wmiServerCredentials1.Password;
            string computer = wmiServerCredentials1.SelectedServer;
            string cacheHost = RemoteWmiAvailability.EffectiveHost(computer, username, password);
            if (!RemoteWmiAvailability.AllowAttempt(cacheHost, this)) return;

            bool refresh = false;
            SetBusy(true);
            try
            {
                List<ManagementObject> current = list;
                bool invoked = await Task.Run(() =>
                {
                    ManagementObject service = FindWMIObject(current, name);
                    if (service == null) return false;
                    service.InvokeMethod(method, null);
                    return true;
                });
                if (IsDisposed) return;
                if (invoked)
                {
                    RemoteWmiAvailability.Succeeded(cacheHost);
                    refresh = true;
                }
            }
            catch (Exception error)
            {
                RemoteWmiAvailability.Failed(cacheHost, error);
                Terminals.Logging.Info("Windows Services command failed", error);
                if (!IsDisposed) MessageBox.Show(this, error.Message);
            }
            finally
            {
                if (IsDisposed)
                    DisposeCachedObjects();
                else
                {
                    SetBusy(false);
                    if (refresh) LoadServices(username, password, computer);
                }
            }
        }

        private void pauseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunServiceCommand("PauseService");
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunServiceCommand("StopService");
        }

        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunServiceCommand("StartService");
        }

        private void wmiServerCredentials1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button1_Click(null, null);
            }
        }
    }
}