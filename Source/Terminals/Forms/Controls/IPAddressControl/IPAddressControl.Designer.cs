// Copyright (c) 2007 Michael Chapman

// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:

// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
// CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

namespace Terminals.Forms.Controls
{
    partial class IPAddressControl
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
            if (disposing && (components != null))
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
            this.FieldControl0 = new IPAddressFieldControl();
            this.DotControl0 = new IPAddressDotControl();
            this.FieldControl1 = new IPAddressFieldControl();
            this.DotControl1 = new IPAddressDotControl();
            this.FieldControl2 = new IPAddressFieldControl();
            this.DotControl2 = new IPAddressDotControl();
            this.FieldControl3 = new IPAddressFieldControl();
            this.SuspendLayout();
            //
            // FieldControl0
            //
            this.FieldControl0.Name = "FieldControl0";
            //
            // DotControl0
            //
            this.DotControl0.Name = "DotControl0";
            //
            // FieldControl1
            //
            this.FieldControl1.Name = "FieldControl1";
            //
            // DotControl1
            //
            this.DotControl1.Name = "DotControl1";
            //
            // FieldControl2
            //
            this.FieldControl2.Name = "FieldControl2";
            //
            // DotControl2
            //
            this.DotControl2.Name = "DotControl2";
            //
            // FieldControl3
            //
            this.FieldControl3.Name = "FieldControl3";
            //
            // IPAddressControl
            //
            this.Controls.Add(this.FieldControl0);
            this.Controls.Add(this.DotControl0);
            this.Controls.Add(this.FieldControl1);
            this.Controls.Add(this.DotControl1);
            this.Controls.Add(this.FieldControl2);
            this.Controls.Add(this.DotControl2);
            this.Controls.Add(this.FieldControl3);
            this.Name = "IPAddressControl";
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.IPAddressControl_DragEnter);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.IPAddressControl_DragDrop);
            this.ResumeLayout(false);
        }

        #endregion

        private IPAddressFieldControl FieldControl0;
        private IPAddressDotControl DotControl0;
        private IPAddressFieldControl FieldControl1;
        private IPAddressDotControl DotControl1;
        private IPAddressFieldControl FieldControl2;
        private IPAddressDotControl DotControl2;
        private IPAddressFieldControl FieldControl3;
    }
}
