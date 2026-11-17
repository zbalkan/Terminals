using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Terminals.Forms
{
    partial class LegacyHttpFavoritesDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.intro = new System.Windows.Forms.Label();
            this.remove = new System.Windows.Forms.RadioButton();
            this.convert = new System.Windows.Forms.RadioButton();
            this.notice = new System.Windows.Forms.Label();
            this.apply = new System.Windows.Forms.Button();
            this.cancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // intro
            //
            this.intro.Location = new System.Drawing.Point(18, 16);
            this.intro.Name = "intro";
            this.intro.Size = new System.Drawing.Size(540, 65);
            this.intro.TabIndex = 0;
            this.intro.Text = "Legacy HTTP favorites were found. HTTP connections are no longer supported. Choose what to do with them. Nothing will change if you cancel.";
            //
            // remove
            //
            this.remove.Location = new System.Drawing.Point(20, 90);
            this.remove.Name = "remove";
            this.remove.Size = new System.Drawing.Size(530, 40);
            this.remove.TabIndex = 1;
            this.remove.Text = "Remove HTTP favorites and clean up the empty HTTP group";
            this.remove.CheckedChanged += new System.EventHandler(this.MigrationChoiceChanged);
            //
            // convert
            //
            this.convert.Location = new System.Drawing.Point(20, 135);
            this.convert.Name = "convert";
            this.convert.Size = new System.Drawing.Size(530, 45);
            this.convert.TabIndex = 2;
            this.convert.Text = "Convert favorites to HTTPS and append them to the HTTPS group";
            this.convert.CheckedChanged += new System.EventHandler(this.MigrationChoiceChanged);
            //
            // notice
            //
            this.notice.Location = new System.Drawing.Point(38, 185);
            this.notice.Name = "notice";
            this.notice.Size = new System.Drawing.Size(510, 35);
            this.notice.TabIndex = 3;
            this.notice.Text = "Conversion does not verify HTTPS availability. Default port 80 becomes 443; custom ports remain unchanged.";
            //
            // apply
            //
            this.apply.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.apply.Enabled = false;
            this.apply.Location = new System.Drawing.Point(386, 232);
            this.apply.Name = "apply";
            this.apply.Size = new System.Drawing.Size(80, 27);
            this.apply.TabIndex = 4;
            this.apply.Text = "Apply";
            this.apply.UseVisualStyleBackColor = true;
            //
            // cancel
            //
            this.cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancel.Location = new System.Drawing.Point(474, 232);
            this.cancel.Name = "cancel";
            this.cancel.Size = new System.Drawing.Size(80, 27);
            this.cancel.TabIndex = 5;
            this.cancel.Text = "Cancel";
            this.cancel.UseVisualStyleBackColor = true;
            //
            // LegacyHttpFavoritesDialog
            //
            this.AcceptButton = this.apply;
            this.CancelButton = this.cancel;
            this.ClientSize = new System.Drawing.Size(580, 270);
            this.Controls.Add(this.intro);
            this.Controls.Add(this.remove);
            this.Controls.Add(this.convert);
            this.Controls.Add(this.notice);
            this.Controls.Add(this.apply);
            this.Controls.Add(this.cancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LegacyHttpFavoritesDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Legacy HTTP favorites";
            this.ResumeLayout(false);
        }

        #endregion

        private Label intro;
        private RadioButton remove;
        private RadioButton convert;
        private Label notice;
        private Button apply;
        private Button cancel;
    }
}
