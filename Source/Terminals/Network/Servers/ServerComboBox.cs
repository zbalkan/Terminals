using System;
using System.Windows.Forms;

namespace NetworkManagement
{
	/// <summary>
	/// A ComboBox that uses Servers object to populate itself
	/// with a list of servers.
	/// </summary>
	public partial class ServerComboBox : ComboBox
	{
		private Servers servers;
		private bool autoRefresh;

		#region Properties
		/// <summary>
		/// Server Type to search.  Can be one or more.
		/// </summary>
		public ServerType ServerType
		{
			get { return servers.Type; }
			set
			{
				servers.Type = value;
				if ( autoRefresh )
					this.Refresh();
			}
		}

		/// <summary>
		/// Domain name to search.  Set to <code>null</code> for all.
		/// </summary>
		public string DomainName
		{
			get { return servers.DomainName; }
			set
			{
				servers.DomainName = value;
				if ( autoRefresh )
					this.Refresh();
			}
		}

		/// <summary>
		/// If true, any changes to DomainName or ServerType will
		/// cause the combobox to refresh it's data (default = false);
		/// </summary>
		public bool AutoRefresh
		{
			get { return autoRefresh; }
			set { autoRefresh = value; }
		}
		#endregion

		/// <summary>
		/// 
		/// </summary>
		public ServerComboBox()
		{
			InitializeComponent();
			servers = new Servers();
			autoRefresh = false;
		}

		/// <summary>
		/// Refreshes the ComboBox's data by enumerating the server list
		/// </summary>
		public override void Refresh()
		{
			base.Refresh();

			BeginUpdate();
			Items.Clear();
			foreach (String name in servers)
			{
				Items.Add(name);
			}
			EndUpdate();
		}
	}
}
