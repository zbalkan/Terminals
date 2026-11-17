using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace WMITestClient
{
    /// <summary>
    /// Summary description for LoginForm.
    /// </summary>
    internal partial class LoginForm : System.Windows.Forms.Form
    {
        private bool isCancelled = true;

        public LoginForm()
        {
            InitializeComponent();
        }

        #region Properties

        public bool Cancelled
        {
            get
            {
                return this.isCancelled;
            }

            set
            {
                this.isCancelled = value;
            }
        }

        public string MachineName
        {
            get
            {
                return this.MachineNameTextBox.Text;
            }

            set
            {
                this.MachineNameTextBox.Text = value;
            }
        }

        public string UserName
        {
            get
            {
                return this.UsernameTextBox.Text;
            }

            set
            {
                this.UsernameTextBox.Text = value;
            }
        }

        public string Password
        {
            get
            {
                return this.PasswordTextBox.Text;
            }

            set
            {
                this.PasswordTextBox.Text = value;
            }
        }

        #endregion

        private void CancelButton_Click(object sender, System.EventArgs e)
        {
            this.Visible = false;
        }

        private void LoginButton_Click(object sender, System.EventArgs e)
        {

            bool success = false;

            if (this.MachineNameTextBox.Text == string.Empty)
            {
                this.MachineNameTextBox.Text = @"\\localhost\root\cimv2";
            }
            else
            {
                if (!this.MachineNameTextBox.Text.StartsWith(@"\\")) 
                    this.MachineNameTextBox.Text = @"\\" + this.MachineNameTextBox.Text;
            }

            if (!this.MachineNameTextBox.Text.StartsWith(@"\\localhost"))
            {
                if (this.UsernameTextBox.Text != string.Empty && this.PasswordTextBox.Text != string.Empty && this.MachineNameTextBox.Text != string.Empty)
                {
                    try
                    {
                        System.Management.ConnectionOptions oConn = new System.Management.ConnectionOptions();
                        oConn.Username = this.UsernameTextBox.Text;
                        oConn.Password = this.PasswordTextBox.Text;
                        oConn.Impersonation = System.Management.ImpersonationLevel.Impersonate;
                        //oConn.Authority = this.MachineNameTextBox.Text;
                        oConn.Authentication = System.Management.AuthenticationLevel.Connect;
                        System.Management.ManagementScope oMs = new System.Management.ManagementScope(this.MachineNameTextBox.Text, oConn);
                        oMs.Path = new System.Management.ManagementPath( this.MachineNameTextBox.Text );
                        oMs.Connect();
                        success = oMs.IsConnected;
                    }
                    catch (Exception exc)
                    {
                        Terminals.Logging.Info("Login Failed", exc);
                        MessageBox.Show(exc.Message);
                    }

                    if (success)
                    {
                        this.Cancelled = false;
                        this.Visible = false;
                    }
                }
            }
            else
            {
                this.Cancelled = false;
                this.Visible = false;
            }
        }

        private void LoginForm_Load(object sender, EventArgs e)
        {

            if (this.MachineNameTextBox.Text == string.Empty)
            {
                this.MachineNameTextBox.Text = @"\\localhost\root\cimv2";
            }
            else
            {
                if (!this.MachineNameTextBox.Text.StartsWith(@"\\"))
                    this.MachineNameTextBox.Text = @"\\" + this.MachineNameTextBox.Text;
            }
        }
    }
}