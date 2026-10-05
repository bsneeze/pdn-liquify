using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.Drawing;
using pyrochild.effects.common;

namespace pyrochild.effects.liquify
{
    partial class ConfigDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void OnDispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                    components.Dispose();
                StopRenderer();
                DisposeLayerBackgrounds();
                if (historystack != null)
                    historystack.Dispose();
                if (surface != null)
                    surface.Dispose();
                if (source != null)
                    source.Dispose();
            }
            base.OnDispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.toolPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.push = new System.Windows.Forms.RadioButton();
            this.twistleft = new System.Windows.Forms.RadioButton();
            this.twistright = new System.Windows.Forms.RadioButton();
            this.bloat = new System.Windows.Forms.RadioButton();
            this.pucker = new System.Windows.Forms.RadioButton();
            this.reconstruct = new System.Windows.Forms.RadioButton();
            this.resetAll = new System.Windows.Forms.Button();
            this.freeze = new System.Windows.Forms.RadioButton();
            this.thaw = new System.Windows.Forms.RadioButton();
            this.clearMask = new System.Windows.Forms.Button();
            this.invertMask = new System.Windows.Forms.Button();
            this.load = new System.Windows.Forms.Button();
            this.save = new System.Windows.Forms.Button();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.viewMenu = new System.Windows.Forms.ToolStripDropDownButton();
            this.showMask = new System.Windows.Forms.ToolStripMenuItem();
            this.meshSmall = new System.Windows.Forms.ToolStripMenuItem();
            this.meshLarge = new System.Windows.Forms.ToolStripMenuItem();
            this.viewSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.layersAbove = new System.Windows.Forms.ToolStripMenuItem();
            this.original = new System.Windows.Forms.ToolStripButton();
            this.allLayers = new System.Windows.Forms.ToolStripButton();
            this.settingStrip = new DoubleBufferedToolStrip();
            this.brushSizeSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.brushSizeLabel = new System.Windows.Forms.ToolStripLabel();
            this.brushSizeDecrement = new System.Windows.Forms.ToolStripButton();
            this.brushSize = new System.Windows.Forms.ToolStripComboBox();
            this.brushSizeIncrement = new System.Windows.Forms.ToolStripButton();
            this.brushSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.pressureLabel = new System.Windows.Forms.ToolStripLabel();
            this.densityLabel = new System.Windows.Forms.ToolStripLabel();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.undo = new System.Windows.Forms.ToolStripButton();
            this.redo = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.zoomOut = new System.Windows.Forms.ToolStripButton();
            this.zoom = new System.Windows.Forms.ToolStripComboBox();
            this.zoomIn = new System.Windows.Forms.ToolStripButton();
            this.panel1 = new System.Windows.Forms.Panel();
            this.ok = new System.Windows.Forms.Button();
            this.cancel = new System.Windows.Forms.Button();
            this.donate = new System.Windows.Forms.LinkLabel();
            this.status = new System.Windows.Forms.Label();
            this.tooltip = new System.Windows.Forms.ToolTip(this.components);
            this.canvas = new pyrochild.effects.common.CanvasPanel();
            this.toolPanel.SuspendLayout();
            this.settingStrip.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolPanel
            // 
            this.toolPanel.Controls.Add(this.push);
            this.toolPanel.Controls.Add(this.twistleft);
            this.toolPanel.Controls.Add(this.twistright);
            this.toolPanel.Controls.Add(this.bloat);
            this.toolPanel.Controls.Add(this.pucker);
            this.toolPanel.Controls.Add(this.reconstruct);
            this.toolPanel.Controls.Add(this.resetAll);
            this.toolPanel.Controls.Add(this.freeze);
            this.toolPanel.Controls.Add(this.thaw);
            this.toolPanel.Controls.Add(this.clearMask);
            this.toolPanel.Controls.Add(this.invertMask);
            this.toolPanel.Controls.Add(this.load);
            this.toolPanel.Controls.Add(this.save);
            this.toolPanel.Dock = System.Windows.Forms.DockStyle.Left;
            this.toolPanel.Location = new System.Drawing.Point(0, 25);
            this.toolPanel.Name = "toolPanel";
            this.toolPanel.Size = new System.Drawing.Size(30, 417);
            this.toolPanel.TabIndex = 0;
            // 
            // push
            // 
            this.push.Appearance = System.Windows.Forms.Appearance.Button;
            this.push.Location = new System.Drawing.Point(2, 0);
            this.push.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.push.Name = "push";
            this.push.Size = new System.Drawing.Size(26, 26);
            this.push.TabIndex = 0;
            this.push.TabStop = true;
            this.push.UseVisualStyleBackColor = true;
            // 
            // twistleft
            // 
            this.twistleft.Appearance = System.Windows.Forms.Appearance.Button;
            this.twistleft.Location = new System.Drawing.Point(2, 26);
            this.twistleft.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.twistleft.Name = "twistleft";
            this.twistleft.Size = new System.Drawing.Size(26, 26);
            this.twistleft.TabIndex = 1;
            this.twistleft.TabStop = true;
            this.twistleft.Text = " ";
            this.twistleft.UseVisualStyleBackColor = true;
            // 
            // twistright
            // 
            this.twistright.Appearance = System.Windows.Forms.Appearance.Button;
            this.twistright.Location = new System.Drawing.Point(2, 52);
            this.twistright.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.twistright.Name = "twistright";
            this.twistright.Size = new System.Drawing.Size(26, 26);
            this.twistright.TabIndex = 2;
            this.twistright.TabStop = true;
            this.twistright.Text = " ";
            this.twistright.UseVisualStyleBackColor = true;
            // 
            // bloat
            // 
            this.bloat.Appearance = System.Windows.Forms.Appearance.Button;
            this.bloat.Location = new System.Drawing.Point(2, 78);
            this.bloat.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.bloat.Name = "bloat";
            this.bloat.Size = new System.Drawing.Size(26, 26);
            this.bloat.TabIndex = 3;
            this.bloat.TabStop = true;
            this.bloat.Text = " ";
            this.bloat.UseVisualStyleBackColor = true;
            // 
            // pucker
            // 
            this.pucker.Appearance = System.Windows.Forms.Appearance.Button;
            this.pucker.Location = new System.Drawing.Point(2, 104);
            this.pucker.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.pucker.Name = "pucker";
            this.pucker.Size = new System.Drawing.Size(26, 26);
            this.pucker.TabIndex = 4;
            this.pucker.TabStop = true;
            this.pucker.Text = " ";
            this.pucker.UseVisualStyleBackColor = true;
            // 
            // reconstruct
            // 
            this.reconstruct.Appearance = System.Windows.Forms.Appearance.Button;
            this.reconstruct.Location = new System.Drawing.Point(2, 144);
            this.reconstruct.Margin = new System.Windows.Forms.Padding(2, 14, 2, 0);
            this.reconstruct.Name = "reconstruct";
            this.reconstruct.Size = new System.Drawing.Size(26, 26);
            this.reconstruct.TabIndex = 5;
            this.reconstruct.TabStop = true;
            this.reconstruct.Text = " ";
            this.reconstruct.UseVisualStyleBackColor = true;
            // 
            // resetAll
            // 
            this.resetAll.Location = new System.Drawing.Point(2, 170);
            this.resetAll.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.resetAll.Name = "resetAll";
            this.resetAll.Size = new System.Drawing.Size(26, 26);
            this.resetAll.TabIndex = 6;
            this.resetAll.UseVisualStyleBackColor = true;
            this.resetAll.Click += new System.EventHandler(this.resetAll_Click);
            // 
            // freeze
            // 
            this.freeze.Appearance = System.Windows.Forms.Appearance.Button;
            this.freeze.Location = new System.Drawing.Point(2, 184);
            this.freeze.Margin = new System.Windows.Forms.Padding(2, 14, 2, 0);
            this.freeze.Name = "freeze";
            this.freeze.Size = new System.Drawing.Size(26, 26);
            this.freeze.TabIndex = 7;
            this.freeze.TabStop = true;
            this.freeze.UseVisualStyleBackColor = true;
            // 
            // thaw
            // 
            this.thaw.Appearance = System.Windows.Forms.Appearance.Button;
            this.thaw.Location = new System.Drawing.Point(2, 210);
            this.thaw.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.thaw.Name = "thaw";
            this.thaw.Size = new System.Drawing.Size(26, 26);
            this.thaw.TabIndex = 8;
            this.thaw.TabStop = true;
            this.thaw.UseVisualStyleBackColor = true;
            // 
            // clearMask
            // 
            this.clearMask.Location = new System.Drawing.Point(2, 236);
            this.clearMask.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.clearMask.Name = "clearMask";
            this.clearMask.Size = new System.Drawing.Size(26, 26);
            this.clearMask.TabIndex = 9;
            this.clearMask.UseVisualStyleBackColor = true;
            this.clearMask.Click += new System.EventHandler(this.clearMask_Click);
            // 
            // invertMask
            // 
            this.invertMask.Location = new System.Drawing.Point(2, 262);
            this.invertMask.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.invertMask.Name = "invertMask";
            this.invertMask.Size = new System.Drawing.Size(26, 26);
            this.invertMask.TabIndex = 10;
            this.invertMask.UseVisualStyleBackColor = true;
            this.invertMask.Click += new System.EventHandler(this.invertMask_Click);
            // 
            // load
            // 
            this.load.Location = new System.Drawing.Point(2, 302);
            this.load.Margin = new System.Windows.Forms.Padding(2, 14, 2, 0);
            this.load.Name = "load";
            this.load.Size = new System.Drawing.Size(26, 26);
            this.load.TabIndex = 11;
            this.load.UseVisualStyleBackColor = true;
            this.load.Click += new System.EventHandler(this.load_Click);
            // 
            // save
            // 
            this.save.Location = new System.Drawing.Point(2, 328);
            this.save.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.save.Name = "save";
            this.save.Size = new System.Drawing.Size(26, 26);
            this.save.TabIndex = 12;
            this.save.UseVisualStyleBackColor = true;
            this.save.Click += new System.EventHandler(this.save_Click);
            // 
            // settingStrip
            // 
            this.settingStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.settingStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.brushSizeSeparator,
            this.brushSizeLabel,
            this.brushSizeDecrement,
            this.brushSize,
            this.brushSizeIncrement,
            this.brushSeparator,
            this.pressureLabel,
            this.densityLabel,
            this.toolStripSeparator1,
            this.undo,
            this.redo,
            this.toolStripSeparator2,
            this.zoomOut,
            this.zoom,
            this.zoomIn,
            this.toolStripSeparator3,
            this.viewMenu,
            this.original,
            this.allLayers});
            this.settingStrip.Location = new System.Drawing.Point(0, 0);
            this.settingStrip.Name = "settingStrip";
            this.settingStrip.Size = new System.Drawing.Size(624, 25);
            this.settingStrip.TabIndex = 3;
            this.settingStrip.Text = "toolStrip1";
            // 
            // brushSizeSeparator
            // 
            this.brushSizeSeparator.Name = "brushSizeSeparator";
            this.brushSizeSeparator.Size = new System.Drawing.Size(6, 25);
            // 
            // brushSizeLabel
            // 
            this.brushSizeLabel.Name = "brushSizeLabel";
            this.brushSizeLabel.Size = new System.Drawing.Size(30, 22);
            this.brushSizeLabel.Text = "Size:";
            // 
            // brushSizeDecrement
            // 
            this.brushSizeDecrement.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.brushSizeDecrement.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.brushSizeDecrement.Name = "brushSizeDecrement";
            this.brushSizeDecrement.Size = new System.Drawing.Size(23, 22);
            this.brushSizeDecrement.Click += new System.EventHandler(this.brushSizeDecrement_Click);
            // 
            // brushSize
            // 
            this.brushSize.AutoSize = false;
            this.brushSize.Name = "brushSize";
            this.brushSize.Size = new System.Drawing.Size(44, 23);
            this.brushSize.Validating += new System.ComponentModel.CancelEventHandler(this.brushSize_Validating);
            this.brushSize.TextChanged += new System.EventHandler(this.brushSize_Validating);
            this.brushSize.LostFocus += new System.EventHandler(this.brushSize_LostFocus);
            // 
            // brushSizeIncrement
            // 
            this.brushSizeIncrement.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.brushSizeIncrement.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.brushSizeIncrement.Name = "brushSizeIncrement";
            this.brushSizeIncrement.Size = new System.Drawing.Size(23, 22);
            this.brushSizeIncrement.Click += new System.EventHandler(this.brushSizeIncrement_Click);
            // 
            // brushSeparator
            // 
            this.brushSeparator.Name = "brushSeparator";
            this.brushSeparator.Size = new System.Drawing.Size(6, 25);
            // 
            // pressureLabel
            // 
            this.pressureLabel.Name = "pressureLabel";
            this.pressureLabel.Size = new System.Drawing.Size(54, 22);
            this.pressureLabel.Text = "Pressure:";
            // 
            // densityLabel
            // 
            this.densityLabel.Name = "densityLabel";
            this.densityLabel.Size = new System.Drawing.Size(49, 22);
            this.densityLabel.Text = "Density:";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // undo
            // 
            this.undo.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.undo.Enabled = false;
            this.undo.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.undo.Name = "undo";
            this.undo.Size = new System.Drawing.Size(23, 22);
            this.undo.Click += new System.EventHandler(this.undo_Click);
            // 
            // redo
            // 
            this.redo.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.redo.Enabled = false;
            this.redo.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.redo.Name = "redo";
            this.redo.Size = new System.Drawing.Size(23, 22);
            this.redo.Click += new System.EventHandler(this.redo_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
            // 
            // zoomOut
            // 
            this.zoomOut.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.zoomOut.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.zoomOut.Name = "zoomOut";
            this.zoomOut.Size = new System.Drawing.Size(23, 22);
            this.zoomOut.Click += new System.EventHandler(this.zoomOut_Click);
            // 
            // zoom
            // 
            this.zoom.AutoSize = false;
            this.zoom.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.zoom.Name = "zoom";
            this.zoom.Size = new System.Drawing.Size(88, 23);
            this.zoom.SelectedIndexChanged += new System.EventHandler(this.zoom_SelectedIndexChanged);
            // 
            // zoomIn
            // 
            this.zoomIn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.zoomIn.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.zoomIn.Name = "zoomIn";
            this.zoomIn.Size = new System.Drawing.Size(23, 22);
            this.zoomIn.Click += new System.EventHandler(this.zoomIn_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(6, 25);
            // 
            // viewMenu
            // 
            this.viewMenu.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.viewMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.showMask,
            this.meshSmall,
            this.meshLarge,
            this.viewSeparator,
            this.layersAbove});
            this.viewMenu.Name = "viewMenu";
            this.viewMenu.Size = new System.Drawing.Size(45, 22);
            this.viewMenu.Text = "View";
            // 
            // showMask
            // 
            this.showMask.Checked = true;
            this.showMask.CheckOnClick = true;
            this.showMask.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showMask.Name = "showMask";
            this.showMask.ShortcutKeyDisplayString = "M";
            this.showMask.Size = new System.Drawing.Size(180, 22);
            this.showMask.Text = "Frozen areas";
            this.showMask.Click += new System.EventHandler(this.showMask_Click);
            // 
            // meshSmall
            // 
            this.meshSmall.CheckOnClick = true;
            this.meshSmall.Name = "meshSmall";
            this.meshSmall.Size = new System.Drawing.Size(180, 22);
            this.meshSmall.Text = "Mesh grid, fine";
            this.meshSmall.Click += new System.EventHandler(this.mesh_Click);
            // 
            // meshLarge
            // 
            this.meshLarge.CheckOnClick = true;
            this.meshLarge.Name = "meshLarge";
            this.meshLarge.Size = new System.Drawing.Size(180, 22);
            this.meshLarge.Text = "Mesh grid, coarse";
            this.meshLarge.Click += new System.EventHandler(this.mesh_Click);
            // 
            // viewSeparator
            // 
            this.viewSeparator.Name = "viewSeparator";
            this.viewSeparator.Size = new System.Drawing.Size(177, 6);
            // 
            // layersAbove
            // 
            this.layersAbove.CheckOnClick = true;
            this.layersAbove.Name = "layersAbove";
            this.layersAbove.Size = new System.Drawing.Size(180, 22);
            this.layersAbove.Text = "Layers above";
            this.layersAbove.Click += new System.EventHandler(this.layersAbove_Click);
            // 
            // original
            // 
            this.original.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.original.Name = "original";
            this.original.Size = new System.Drawing.Size(55, 22);
            this.original.Text = "Original";
            // 
            // allLayers
            // 
            this.allLayers.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.allLayers.Name = "allLayers";
            this.allLayers.Size = new System.Drawing.Size(62, 22);
            this.allLayers.Text = "All layers";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.ok);
            this.panel1.Controls.Add(this.cancel);
            this.panel1.Controls.Add(this.donate);
            this.panel1.Controls.Add(this.status);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(30, 412);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(594, 30);
            this.panel1.TabIndex = 2;
            // 
            // ok
            // 
            this.ok.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.ok.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.ok.Location = new System.Drawing.Point(435, 4);
            this.ok.Name = "ok";
            this.ok.Size = new System.Drawing.Size(75, 23);
            this.ok.TabIndex = 0;
            this.ok.Text = "OK";
            this.ok.UseVisualStyleBackColor = true;
            this.ok.Click += new System.EventHandler(this.ok_Click);
            // 
            // cancel
            // 
            this.cancel.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancel.Location = new System.Drawing.Point(516, 4);
            this.cancel.Name = "cancel";
            this.cancel.Size = new System.Drawing.Size(75, 23);
            this.cancel.TabIndex = 1;
            this.cancel.Text = "Cancel";
            this.cancel.UseVisualStyleBackColor = true;
            // 
            // donate
            // 
            this.donate.AutoSize = true;
            this.donate.Location = new System.Drawing.Point(6, 10);
            this.donate.Name = "donate";
            this.donate.Size = new System.Drawing.Size(45, 13);
            this.donate.TabIndex = 2;
            this.donate.TabStop = true;
            this.donate.Text = "Donate!";
            this.donate.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.donate_LinkClicked);
            // 
            // status
            // 
            this.status.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.status.AutoEllipsis = true;
            this.status.Location = new System.Drawing.Point(60, 4);
            this.status.Name = "status";
            this.status.Size = new System.Drawing.Size(369, 23);
            this.status.TabIndex = 3;
            this.status.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // canvas
            // 
            this.canvas.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.canvas.AutoScroll = true;
            this.canvas.AutoScrollMargin = new System.Drawing.Size(10, 10);
            this.canvas.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(127)))), ((int)(((byte)(127)))), ((int)(((byte)(127)))));
            this.canvas.BrushSize = 0;
            this.canvas.CanvasBackColor = System.Drawing.Color.Transparent;
            this.canvas.Location = new System.Drawing.Point(30, 25);
            this.canvas.MouseHoldInterval = 50;
            this.canvas.Name = "canvas";
            this.canvas.Selection = null;
            this.canvas.Size = new System.Drawing.Size(594, 387);
            this.canvas.Surface = null;
            this.canvas.TabIndex = 1;
            this.canvas.TabStop = false;
            this.canvas.ZoomFactor = 1F;
            this.canvas.CanvasMouseDown += new System.EventHandler<pyrochild.effects.common.CanvasMouseEventArgs>(this.canvas_CanvasMouseDown);
            this.canvas.CanvasMouseMove += new System.EventHandler<pyrochild.effects.common.CanvasMouseEventArgs>(this.canvas_CanvasMouseMove);
            this.canvas.CanvasMouseUp += new System.EventHandler<pyrochild.effects.common.CanvasMouseEventArgs>(this.canvas_CanvasMouseUp);
            this.canvas.CanvasMouseHold += new System.EventHandler<pyrochild.effects.common.CanvasMouseEventArgs>(this.canvas_CanvasMouseHold);
            this.canvas.ZoomFactorChanged += new System.EventHandler(this.canvas_ZoomFactorChanged);
            // 
            // ConfigDialog
            // 
            this.AcceptButton = this.ok;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this.cancel;
            this.ClientSize = new System.Drawing.Size(624, 442);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Controls.Add(this.canvas);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.toolPanel);
            this.Controls.Add(this.settingStrip);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.KeyPreview = true;
            this.Location = new System.Drawing.Point(0, 0);
            this.MaximizeBox = true;
            this.MinimumSize = new System.Drawing.Size(640, 480);
            this.Name = "ConfigDialog";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Auto;
            this.Load += new System.EventHandler(this.ConfigDialog_Load);
            this.toolPanel.ResumeLayout(false);
            this.settingStrip.ResumeLayout(false);
            this.settingStrip.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        
        #endregion

        private FlowLayoutPanel toolPanel;
        private ToolStrip settingStrip;
        private RadioButton push;
        private RadioButton reconstruct;
        private RadioButton bloat;
        private RadioButton pucker;
        private Panel panel1;
        private Button ok;
        private Button cancel;
        private LinkLabel donate;
        private Label status;
        private ToolStripSeparator brushSizeSeparator;
        private ToolStripLabel brushSizeLabel;
        private ToolStripButton brushSizeDecrement;
        private ToolStripComboBox brushSize;
        private ToolStripButton brushSizeIncrement;
        private ToolStripSeparator brushSeparator;
        private ToolStripLabel pressureLabel;
        private ToolStripLabel densityLabel;
        private CanvasPanel canvas;
        private Button load;
        private Button save;
        private RadioButton twistleft;
        private RadioButton twistright;
        private ToolTip tooltip;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripButton undo;
        private ToolStripButton redo;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripButton zoomOut;
        private ToolStripComboBox zoom;
        private ToolStripButton zoomIn;
        private RadioButton freeze;
        private RadioButton thaw;
        private Button clearMask;
        private Button invertMask;
        private ToolStripSeparator toolStripSeparator3;
        private Button resetAll;
        private ToolStripDropDownButton viewMenu;
        private ToolStripMenuItem showMask;
        private ToolStripSeparator viewSeparator;
        private ToolStripMenuItem layersAbove;
        private ToolStripButton original;
        private ToolStripButton allLayers;
        private ToolStripMenuItem meshSmall;
        private ToolStripMenuItem meshLarge;
    }
}