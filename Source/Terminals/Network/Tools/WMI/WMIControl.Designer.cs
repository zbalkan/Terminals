using System.ComponentModel;
using System.Windows.Forms;

namespace WMITestClient
{
    internal partial class WMIControl
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components;

        #region Component Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.TreeNode treeNode1 = new System.Windows.Forms.TreeNode("To load-> Double Click");
            this.QueryButton = new System.Windows.Forms.Button();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.StopButton = new System.Windows.Forms.Button();
            this.QueryTextBox = new System.Windows.Forms.ComboBox();
            this.mainMenu1 = new System.Windows.Forms.MainMenu(this.components);
            this.menuItem1 = new System.Windows.Forms.MenuItem();
            this.LoginMenuItem = new System.Windows.Forms.MenuItem();
            this.menuItem2 = new System.Windows.Forms.MenuItem();
            this.SavemenuItem = new System.Windows.Forms.MenuItem();
            this.LoadmenuItem = new System.Windows.Forms.MenuItem();
            this.ClearmenuItem = new System.Windows.Forms.MenuItem();
            this.BasicTreemenuItem = new System.Windows.Forms.MenuItem();
            this.ExitmenuItem = new System.Windows.Forms.MenuItem();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.treeView2 = new System.Windows.Forms.TreeView();
            this.ConnectButton = new System.Windows.Forms.Button();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.panel1 = new System.Windows.Forms.Panel();
            this.ConnectionLabel = new System.Windows.Forms.Label();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.SuspendLayout();
            // 
            // QueryButton
            // 
            this.QueryButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.QueryButton.Location = new System.Drawing.Point(592, 7);
            this.QueryButton.Name = "QueryButton";
            this.QueryButton.Size = new System.Drawing.Size(67, 24);
            this.QueryButton.TabIndex = 1;
            this.QueryButton.Text = "&Query";
            this.QueryButton.Click += new System.EventHandler(this.QueryButton_Click);
            // 
            // progressBar1
            // 
            this.progressBar1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.progressBar1.Location = new System.Drawing.Point(0, 81);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(753, 10);
            this.progressBar1.TabIndex = 5;
            // 
            // StopButton
            // 
            this.StopButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.StopButton.Enabled = false;
            this.StopButton.Location = new System.Drawing.Point(667, 7);
            this.StopButton.Name = "StopButton";
            this.StopButton.Size = new System.Drawing.Size(67, 24);
            this.StopButton.TabIndex = 2;
            this.StopButton.Text = "Stop!";
            this.StopButton.Click += new System.EventHandler(this.button1_Click);
            // 
            // QueryTextBox
            // 
            this.QueryTextBox.Dock = System.Windows.Forms.DockStyle.Top;
            this.QueryTextBox.Location = new System.Drawing.Point(0, 0);
            this.QueryTextBox.Name = "QueryTextBox";
            this.QueryTextBox.Size = new System.Drawing.Size(753, 24);
            this.QueryTextBox.TabIndex = 0;
            this.QueryTextBox.Text = "select * from CIM_System";
            this.QueryTextBox.KeyUp += new System.Windows.Forms.KeyEventHandler(this.QueryTextBox_KeyUp);
            // 
            // mainMenu1
            // 
            this.mainMenu1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItem1});
            // 
            // menuItem1
            // 
            this.menuItem1.Index = 0;
            this.menuItem1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.LoginMenuItem,
            this.menuItem2,
            this.BasicTreemenuItem,
            this.ExitmenuItem});
            this.menuItem1.Text = "&Options";
            // 
            // LoginMenuItem
            // 
            this.LoginMenuItem.Index = 0;
            this.LoginMenuItem.Text = "&Login";
            this.LoginMenuItem.Click += new System.EventHandler(this.LoginMenuItem_Click);
            // 
            // menuItem2
            // 
            this.menuItem2.Index = 1;
            this.menuItem2.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.SavemenuItem,
            this.LoadmenuItem,
            this.ClearmenuItem});
            this.menuItem2.Text = "&History";
            // 
            // SavemenuItem
            // 
            this.SavemenuItem.Index = 0;
            this.SavemenuItem.Text = "&Save";
            this.SavemenuItem.Click += new System.EventHandler(this.SavemenuItem_Click);
            // 
            // LoadmenuItem
            // 
            this.LoadmenuItem.Index = 1;
            this.LoadmenuItem.Text = "L&oad";
            this.LoadmenuItem.Click += new System.EventHandler(this.LoadmenuItem_Click);
            // 
            // ClearmenuItem
            // 
            this.ClearmenuItem.Index = 2;
            this.ClearmenuItem.Text = "&Clear";
            this.ClearmenuItem.Click += new System.EventHandler(this.ClearmenuItem_Click);
            // 
            // BasicTreemenuItem
            // 
            this.BasicTreemenuItem.Index = 2;
            this.BasicTreemenuItem.Text = "Load Static Class Tree";
            this.BasicTreemenuItem.Click += new System.EventHandler(this.BasicTreemenuItem_Click);
            // 
            // ExitmenuItem
            // 
            this.ExitmenuItem.Index = 3;
            this.ExitmenuItem.Text = "E&xit";
            this.ExitmenuItem.Click += new System.EventHandler(this.ExitmenuItem_Click);
            // 
            // treeView1
            // 
            this.treeView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView1.Location = new System.Drawing.Point(0, 0);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(399, 374);
            this.treeView1.TabIndex = 3;
            this.treeView1.DoubleClick += new System.EventHandler(this.treeView1_DoubleClick);
            // 
            // treeView2
            // 
            this.treeView2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView2.Location = new System.Drawing.Point(0, 0);
            this.treeView2.Name = "treeView2";
            treeNode1.Name = "";
            treeNode1.Text = "To load-> Double Click";
            this.treeView2.Nodes.AddRange(new System.Windows.Forms.TreeNode[] {
            treeNode1});
            this.treeView2.Size = new System.Drawing.Size(350, 374);
            this.treeView2.TabIndex = 4;
            this.treeView2.Click += new System.EventHandler(this.treeView2_Click);
            this.treeView2.DoubleClick += new System.EventHandler(this.treeView2_DoubleClick);
            // 
            // ConnectButton
            // 
            this.ConnectButton.Location = new System.Drawing.Point(4, 7);
            this.ConnectButton.Name = "ConnectButton";
            this.ConnectButton.Size = new System.Drawing.Size(67, 24);
            this.ConnectButton.TabIndex = 3;
            this.ConnectButton.Text = "Connect...";
            this.ConnectButton.UseVisualStyleBackColor = true;
            this.ConnectButton.Click += new System.EventHandler(this.ConnectButton_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.panel1);
            this.splitContainer1.Panel1.Controls.Add(this.QueryTextBox);
            this.splitContainer1.Panel1.Controls.Add(this.progressBar1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer1.Size = new System.Drawing.Size(753, 469);
            this.splitContainer1.SplitterDistance = 91;
            this.splitContainer1.TabIndex = 7;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.ConnectionLabel);
            this.panel1.Controls.Add(this.QueryButton);
            this.panel1.Controls.Add(this.StopButton);
            this.panel1.Controls.Add(this.ConnectButton);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 24);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(753, 57);
            this.panel1.TabIndex = 7;
            // 
            // ConnectionLabel
            // 
            this.ConnectionLabel.AutoSize = true;
            this.ConnectionLabel.Location = new System.Drawing.Point(88, 13);
            this.ConnectionLabel.Name = "ConnectionLabel";
            this.ConnectionLabel.Size = new System.Drawing.Size(141, 17);
            this.ConnectionLabel.TabIndex = 7;
            this.ConnectionLabel.Text = "\\\\localhost\\root\\cimv2";
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.treeView2);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.treeView1);
            this.splitContainer2.Size = new System.Drawing.Size(753, 374);
            this.splitContainer2.SplitterDistance = 350;
            this.splitContainer2.TabIndex = 5;
            // 
            // WMIControl
            // 
            this.Controls.Add(this.splitContainer1);
            this.Name = "WMIControl";
            this.Size = new System.Drawing.Size(753, 469);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.Resize += new System.EventHandler(this.Form1_Resize);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        #endregion

        private System.Windows.Forms.Button QueryButton;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Button StopButton;
        private System.Windows.Forms.ComboBox QueryTextBox;
        private System.Windows.Forms.MainMenu mainMenu1;
        private System.Windows.Forms.MenuItem menuItem1;
        private System.Windows.Forms.MenuItem menuItem2;
        private System.Windows.Forms.MenuItem LoginMenuItem;
        private System.Windows.Forms.MenuItem ExitmenuItem;
        private System.Windows.Forms.MenuItem SavemenuItem;
        private System.Windows.Forms.MenuItem LoadmenuItem;
        private System.Windows.Forms.MenuItem ClearmenuItem;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.MenuItem BasicTreemenuItem;
        private System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.TreeView treeView2;
        private Button ConnectButton;
        private SplitContainer splitContainer1;
        private Panel panel1;
        private Label ConnectionLabel;
        private SplitContainer splitContainer2;
    }
}
