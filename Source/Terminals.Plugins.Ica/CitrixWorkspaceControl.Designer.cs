namespace Terminals.Forms.EditFavorite
{
    partial class CitrixWorkspaceControl
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.modeLabel = new System.Windows.Forms.Label();
            this.mode = new System.Windows.Forms.ComboBox();
            this.resourceLabel = new System.Windows.Forms.Label();
            this.resource = new System.Windows.Forms.TextBox();
            this.guidanceLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // modeLabel
            //
            this.modeLabel.Location = new System.Drawing.Point(12, 22);
            this.modeLabel.Name = "modeLabel";
            this.modeLabel.Size = new System.Drawing.Size(120, 20);
            this.modeLabel.TabIndex = 0;
            this.modeLabel.Text = "Launch mode:";
            //
            // mode
            //
            this.mode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.mode.FormattingEnabled = true;
            this.mode.Items.AddRange(new object[] {
                "Workspace / Cloud (SSO)",
                "StoreFront / on-premises"});
            this.mode.Location = new System.Drawing.Point(140, 18);
            this.mode.Name = "mode";
            this.mode.Size = new System.Drawing.Size(260, 21);
            this.mode.TabIndex = 1;
            //
            // resourceLabel
            //
            this.resourceLabel.Location = new System.Drawing.Point(12, 57);
            this.resourceLabel.Name = "resourceLabel";
            this.resourceLabel.Size = new System.Drawing.Size(125, 20);
            this.resourceLabel.TabIndex = 2;
            this.resourceLabel.Text = "Published resource:";
            //
            // resource
            //
            this.resource.Location = new System.Drawing.Point(140, 53);
            this.resource.Name = "resource";
            this.resource.Size = new System.Drawing.Size(260, 20);
            this.resource.TabIndex = 3;
            //
            // guidanceLabel
            //
            this.guidanceLabel.Location = new System.Drawing.Point(12, 95);
            this.guidanceLabel.Name = "guidanceLabel";
            this.guidanceLabel.Size = new System.Drawing.Size(400, 64);
            this.guidanceLabel.TabIndex = 4;
            this.guidanceLabel.Text = "Set the HTTPS store/discovery URL on the General tab. Citrix Workspace handles authentication.";
            //
            // CitrixWorkspaceControl
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.modeLabel);
            this.Controls.Add(this.mode);
            this.Controls.Add(this.resourceLabel);
            this.Controls.Add(this.resource);
            this.Controls.Add(this.guidanceLabel);
            this.Name = "CitrixWorkspaceControl";
            this.Size = new System.Drawing.Size(150, 150);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label modeLabel;
        private System.Windows.Forms.ComboBox mode;
        private System.Windows.Forms.Label resourceLabel;
        private System.Windows.Forms.TextBox resource;
        private System.Windows.Forms.Label guidanceLabel;
    }
}
