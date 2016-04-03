namespace pyrochild.effects.common
{
    partial class ColorDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ColorDialog));
            this.ok = new System.Windows.Forms.Button();
            this.rupdown = new System.Windows.Forms.NumericUpDown();
            this.hex = new System.Windows.Forms.TextBox();
            this.gupdown = new System.Windows.Forms.NumericUpDown();
            this.bupdown = new System.Windows.Forms.NumericUpDown();
            this.hupdown = new System.Windows.Forms.NumericUpDown();
            this.supdown = new System.Windows.Forms.NumericUpDown();
            this.vupdown = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.cancel = new System.Windows.Forms.Button();
            this.label7 = new System.Windows.Forms.Label();
            this.aupdown = new System.Windows.Forms.NumericUpDown();
            this.aslider = new pyrochild.effects.common.ColorGradientControl();
            this.gslider = new pyrochild.effects.common.ColorGradientControl();
            this.bslider = new pyrochild.effects.common.ColorGradientControl();
            this.hslider = new pyrochild.effects.common.ColorGradientControl();
            this.sslider = new pyrochild.effects.common.ColorGradientControl();
            this.vslider = new pyrochild.effects.common.ColorGradientControl();
            this.rslider = new pyrochild.effects.common.ColorGradientControl();
            this.wheel = new pyrochild.effects.common.ColorWheel();
            ((System.ComponentModel.ISupportInitialize)(this.rupdown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gupdown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bupdown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.hupdown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.supdown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.vupdown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.aupdown)).BeginInit();
            this.SuspendLayout();
            // 
            // ok
            // 
            this.ok.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ok.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.ok.Location = new System.Drawing.Point(200, 196);
            this.ok.Name = "ok";
            this.ok.Size = new System.Drawing.Size(75, 23);
            this.ok.TabIndex = 1;
            this.ok.Text = "OK";
            this.ok.UseVisualStyleBackColor = true;
            // 
            // rupdown
            // 
            this.rupdown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.rupdown.Location = new System.Drawing.Point(300, 12);
            this.rupdown.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.rupdown.Name = "rupdown";
            this.rupdown.Size = new System.Drawing.Size(56, 20);
            this.rupdown.TabIndex = 8;
            this.rupdown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // hex
            // 
            this.hex.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.hex.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.hex.Location = new System.Drawing.Point(75, 201);
            this.hex.MaxLength = 6;
            this.hex.Name = "hex";
            this.hex.Size = new System.Drawing.Size(56, 20);
            this.hex.TabIndex = 9;
            this.hex.Text = "000000";
            this.hex.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // gupdown
            // 
            this.gupdown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.gupdown.Location = new System.Drawing.Point(300, 34);
            this.gupdown.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.gupdown.Name = "gupdown";
            this.gupdown.Size = new System.Drawing.Size(56, 20);
            this.gupdown.TabIndex = 25;
            this.gupdown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // bupdown
            // 
            this.bupdown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.bupdown.Location = new System.Drawing.Point(300, 56);
            this.bupdown.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.bupdown.Name = "bupdown";
            this.bupdown.Size = new System.Drawing.Size(56, 20);
            this.bupdown.TabIndex = 26;
            this.bupdown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // hupdown
            // 
            this.hupdown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.hupdown.Location = new System.Drawing.Point(300, 88);
            this.hupdown.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            this.hupdown.Name = "hupdown";
            this.hupdown.Size = new System.Drawing.Size(56, 20);
            this.hupdown.TabIndex = 27;
            this.hupdown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // supdown
            // 
            this.supdown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.supdown.Location = new System.Drawing.Point(300, 110);
            this.supdown.Name = "supdown";
            this.supdown.Size = new System.Drawing.Size(56, 20);
            this.supdown.TabIndex = 28;
            this.supdown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // vupdown
            // 
            this.vupdown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.vupdown.Location = new System.Drawing.Point(300, 132);
            this.vupdown.Name = "vupdown";
            this.vupdown.Size = new System.Drawing.Size(56, 20);
            this.vupdown.TabIndex = 29;
            this.vupdown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(202, 16);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(15, 13);
            this.label1.TabIndex = 30;
            this.label1.Text = "R";
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(202, 38);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(15, 13);
            this.label2.TabIndex = 31;
            this.label2.Text = "G";
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(202, 92);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(15, 13);
            this.label3.TabIndex = 33;
            this.label3.Text = "H";
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(202, 60);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(14, 13);
            this.label4.TabIndex = 32;
            this.label4.Text = "B";
            // 
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(202, 136);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(14, 13);
            this.label5.TabIndex = 35;
            this.label5.Text = "V";
            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(202, 114);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(14, 13);
            this.label6.TabIndex = 34;
            this.label6.Text = "S";
            // 
            // cancel
            // 
            this.cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancel.Location = new System.Drawing.Point(281, 196);
            this.cancel.Name = "cancel";
            this.cancel.Size = new System.Drawing.Size(75, 23);
            this.cancel.TabIndex = 36;
            this.cancel.Text = "Cancel";
            this.cancel.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            this.label7.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(202, 168);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(14, 13);
            this.label7.TabIndex = 43;
            this.label7.Text = "A";
            // 
            // aupdown
            // 
            this.aupdown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.aupdown.Location = new System.Drawing.Point(300, 164);
            this.aupdown.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.aupdown.Name = "aupdown";
            this.aupdown.Size = new System.Drawing.Size(56, 20);
            this.aupdown.TabIndex = 41;
            this.aupdown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // aslider
            // 
            this.aslider.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.aslider.DrawFarNub = true;
            this.aslider.DrawNearNub = false;
            this.aslider.Gradient = null;
            this.aslider.Location = new System.Drawing.Point(221, 165);
            this.aslider.Name = "aslider";
            this.aslider.Size = new System.Drawing.Size(73, 19);
            this.aslider.TabIndex = 38;
            this.aslider.Value = 0F;
            // 
            // gslider
            // 
            this.gslider.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.gslider.DrawFarNub = true;
            this.gslider.DrawNearNub = false;
            this.gslider.Gradient = null;
            this.gslider.Location = new System.Drawing.Point(221, 35);
            this.gslider.Name = "gslider";
            this.gslider.Size = new System.Drawing.Size(73, 19);
            this.gslider.TabIndex = 24;
            this.gslider.Value = 0F;
            // 
            // bslider
            // 
            this.bslider.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.bslider.DrawFarNub = true;
            this.bslider.DrawNearNub = false;
            this.bslider.Gradient = null;
            this.bslider.Location = new System.Drawing.Point(221, 57);
            this.bslider.Name = "bslider";
            this.bslider.Size = new System.Drawing.Size(73, 19);
            this.bslider.TabIndex = 23;
            this.bslider.Value = 0F;
            // 
            // hslider
            // 
            this.hslider.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.hslider.DrawFarNub = true;
            this.hslider.DrawNearNub = false;
            this.hslider.Gradient = null;
            this.hslider.Location = new System.Drawing.Point(221, 89);
            this.hslider.Name = "hslider";
            this.hslider.Size = new System.Drawing.Size(73, 19);
            this.hslider.TabIndex = 22;
            this.hslider.Value = 0F;
            // 
            // sslider
            // 
            this.sslider.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.sslider.DrawFarNub = true;
            this.sslider.DrawNearNub = false;
            this.sslider.Gradient = null;
            this.sslider.Location = new System.Drawing.Point(221, 111);
            this.sslider.Name = "sslider";
            this.sslider.Size = new System.Drawing.Size(73, 19);
            this.sslider.TabIndex = 21;
            this.sslider.Value = 0F;
            // 
            // vslider
            // 
            this.vslider.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.vslider.DrawFarNub = true;
            this.vslider.DrawNearNub = false;
            this.vslider.Gradient = null;
            this.vslider.Location = new System.Drawing.Point(221, 133);
            this.vslider.Name = "vslider";
            this.vslider.Size = new System.Drawing.Size(73, 19);
            this.vslider.TabIndex = 20;
            this.vslider.Value = 0F;
            // 
            // rslider
            // 
            this.rslider.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.rslider.DrawFarNub = true;
            this.rslider.DrawNearNub = false;
            this.rslider.Gradient = null;
            this.rslider.Location = new System.Drawing.Point(221, 13);
            this.rslider.Name = "rslider";
            this.rslider.Size = new System.Drawing.Size(73, 19);
            this.rslider.TabIndex = 18;
            this.rslider.Value = 0F;
            // 
            // wheel
            // 
            this.wheel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.wheel.Color = ((PaintDotNet.ColorBgra)(resources.GetObject("wheel.Color")));
            this.wheel.Location = new System.Drawing.Point(12, 14);
            this.wheel.Name = "wheel";
            this.wheel.Size = new System.Drawing.Size(182, 181);
            this.wheel.TabIndex = 0;
            // 
            // ColorDialog
            // 
            this.AcceptButton = this.ok;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.CancelButton = this.cancel;
            this.ClientSize = new System.Drawing.Size(368, 231);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.aupdown);
            this.Controls.Add(this.aslider);
            this.Controls.Add(this.cancel);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.vupdown);
            this.Controls.Add(this.supdown);
            this.Controls.Add(this.hupdown);
            this.Controls.Add(this.bupdown);
            this.Controls.Add(this.gupdown);
            this.Controls.Add(this.gslider);
            this.Controls.Add(this.bslider);
            this.Controls.Add(this.hslider);
            this.Controls.Add(this.sslider);
            this.Controls.Add(this.vslider);
            this.Controls.Add(this.rslider);
            this.Controls.Add(this.hex);
            this.Controls.Add(this.rupdown);
            this.Controls.Add(this.ok);
            this.Controls.Add(this.wheel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.HelpButton = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ColorDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            ((System.ComponentModel.ISupportInitialize)(this.rupdown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gupdown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bupdown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.hupdown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.supdown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.vupdown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.aupdown)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ColorWheel wheel;
        private System.Windows.Forms.Button ok;
        private System.Windows.Forms.NumericUpDown rupdown;
        private System.Windows.Forms.TextBox hex;
        private ColorGradientControl rslider;
        private ColorGradientControl vslider;
        private ColorGradientControl sslider;
        private ColorGradientControl hslider;
        private ColorGradientControl bslider;
        private ColorGradientControl gslider;
        private System.Windows.Forms.NumericUpDown gupdown;
        private System.Windows.Forms.NumericUpDown bupdown;
        private System.Windows.Forms.NumericUpDown hupdown;
        private System.Windows.Forms.NumericUpDown supdown;
        private System.Windows.Forms.NumericUpDown vupdown;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button cancel;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.NumericUpDown aupdown;
        private ColorGradientControl aslider;
    }
}