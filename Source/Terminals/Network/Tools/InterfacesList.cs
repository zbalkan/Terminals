using System;
using System.Windows.Forms;

namespace Terminals.Network
{
    internal partial class InterfacesList : UserControl
    {
        public InterfacesList()
        {
            InitializeComponent();
        }

        private void InterfacesList_Load(object sender, EventArgs e)
        {
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dataGridView2.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dataGridView1.DataSource = NetworkInterfaceInventory.GetIPv4Interfaces();
            dataGridView2.DataSource = Terminals.Network.DNS.AdapterInfo.GetAdapters();
        }
    }
}
