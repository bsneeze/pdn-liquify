using PaintDotNet;
using PaintDotNet.Effects;
using pyrochild.effects.common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace pyrochild.effects.liquify
{
    public partial class ConfigDialog : EffectConfigDialog
    {
        private HistoryStack historystack;
        private LiquifyRenderer renderer;
        private const char decPenSizeShortcut = '[';
        private const char decPenSizeBy5Shortcut = (char)27; // Ctrl [ but must also test that Ctrl is down
        private const char incPenSizeShortcut = ']';
        private const char incPenSizeBy5Shortcut = (char)29; // Ctrl ] but must also test that Ctrl is down
        private const char undoShortcut = (char)26;
        private const char redoShortcut = (char)25;
        private Surface surface;
        private SliderControl pressure, density;
        private const int minPenSize = 2;
        private const int maxPenSize = 1500;
        private int[] brushSizes =
            { 
                2, 3, 4, 5, 6, 7, 8, 9, 10, 
                11, 12, 13, 14, 15, 20, 25, 30, 
                35, 40, 45, 50, 60, 70, 80, 90, 100, 125, 150, 200, 300
            };
        private DisplacementMesh mesh;
        private Dictionary<Control, LiquifyMode> rendermodes;
        private LiquifyMode mode;

        public ConfigDialog()
        {
            InitializeComponent();

            this.Text = Liquify.StaticDialogName;

            pressure = new SliderControl();
            pressure.Minimum = .01f;
            density = new SliderControl();

            this.brushSize.ComboBox.SuspendLayout();

            for (int i = 0; i < this.brushSizes.Length; ++i)
            {
                this.brushSize.Items.Add(this.brushSizes[i].ToString());
            }

            this.brushSize.ComboBox.ResumeLayout(false);

            settingStrip.Items.Insert(
                settingStrip.Items.IndexOf(pressureLabel) + 1,
                new ToolStripControlHost(pressure) { AutoSize = false });

            settingStrip.Items.Insert(
                settingStrip.Items.IndexOf(densityLabel) + 1,
                new ToolStripControlHost(density) { AutoSize = false });

            this.zoom.ComboBox.SuspendLayout();

            string percent100 = null;
            for (int i = 0; i < CanvasPanel.ZoomFactors.Length; i++)
            {
                string zoomValueString = (CanvasPanel.ZoomFactors[i] * 100).ToString();
                string zoomItemString = string.Format("{0}%", zoomValueString);

                if (CanvasPanel.ZoomFactors[i] == 1.0)
                {
                    percent100 = zoomItemString;
                }

                this.zoom.Items.Add(zoomItemString);
            }
            this.zoom.ComboBox.ResumeLayout(false);
            this.zoom.Text = percent100;

            foreach (Control control in toolPanel.Controls)
            {
                RadioButton radioButton = control as RadioButton;
                if (radioButton != null)
                    radioButton.CheckedChanged += new EventHandler(toolRadioButton_CheckedChanged);
            }

            InitializeUIImages();
            InitializeTooltips();
        }

        private void InitializeUIImages()
        {
            Type t = typeof(Liquify);

            push.Image = new Bitmap(t, "images.push.png");
            reconstruct.Image = new Bitmap(t, "images.reconstruct.png");
            bloat.Image = new Bitmap(t, "images.bloat.png");
            pucker.Image = new Bitmap(t, "images.pucker.png");
            load.Image = new Bitmap(t, "images.open.png");
            save.Image = new Bitmap(t, "images.save.png");
            twistleft.Image = new Bitmap(t, "images.twistleft.png");
            twistright.Image = new Bitmap(t, "images.twistright.png");
            brushSizeIncrement.Image = new Bitmap(t, "images.plus.png");
            brushSizeDecrement.Image = new Bitmap(t, "images.minus.png");
            undo.Image = new Bitmap(t, "images.undo.png");
            redo.Image = new Bitmap(t, "images.redo.png");
            zoomIn.Image = new Bitmap(t, "images.zoomin.png");
            zoomOut.Image = new Bitmap(t, "images.zoomout.png");
            freeze.Image = new Bitmap(t, "images.freeze.png");
            thaw.Image = new Bitmap(t, "images.thaw.png");
        }

        private void InitializeTooltips()
        {
            tooltip.SetToolTip(push, "Push");
            tooltip.SetToolTip(reconstruct, "Reconstruct");
            tooltip.SetToolTip(bloat, "Bloat");
            tooltip.SetToolTip(pucker, "Pucker");
            tooltip.SetToolTip(twistleft, "Twist left");
            tooltip.SetToolTip(twistright, "Twist right");
            tooltip.SetToolTip(save, "Save mesh");
            tooltip.SetToolTip(load, "Load mesh");
            tooltip.SetToolTip(freeze, "Freeze");
            tooltip.SetToolTip(thaw, "Thaw");
            undo.ToolTipText= "Undo";
            redo.ToolTipText= "Redo";
            brushSizeIncrement.ToolTipText = "Increase brush size";
            brushSizeDecrement.ToolTipText = "Decrease brush size";
            zoomIn.ToolTipText = "Zoom in";
            zoomOut.ToolTipText = "Zoom out";
        }

        private void InitializeRenderer()
        {
            renderer = new LiquifyRenderer(mesh);

            renderer.Invalidated += new InvalidateEventHandler(renderer_Invalidated);
            renderer.MouseDown += new QueuedToolEventHandler(renderer_MouseDown);
            renderer.MouseUp += new QueuedToolEventHandler(renderer_MouseUp);

            rendermodes = new Dictionary<Control, LiquifyMode>();

            rendermodes.Add(push, LiquifyMode.Push);
            rendermodes.Add(reconstruct, LiquifyMode.Reconstruct);
            rendermodes.Add(bloat, LiquifyMode.Bloat);
            rendermodes.Add(pucker, LiquifyMode.Pucker);
            rendermodes.Add(twistleft, LiquifyMode.TwistLeft);
            rendermodes.Add(twistright, LiquifyMode.TwistRight);
            rendermodes.Add(freeze, LiquifyMode.Freeze);
            rendermodes.Add(thaw, LiquifyMode.Thaw);
        }

        private void canvas_ZoomFactorChanged(object sender, EventArgs e)
        {
            zoom.SelectedItem = string.Format("{0}%", canvas.ZoomFactor * 100);
        }

        void renderer_MouseUp(object sender, QueuedToolEventArgs e)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    Action<object, QueuedToolEventArgs> action = renderer_MouseUp;
                    this.Invoke(action, new object[] { sender, e });
                }
                else
                {
                    historystack.AddHistoryItem(mesh, renderer.PopTotalInvalidRect());
                    UpdateHistoryButtons(false);
                    ok.Enabled = true;
                }
            }
            catch (ObjectDisposedException) { }
        }

        void renderer_MouseDown(object sender, QueuedToolEventArgs e)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    Action<object, QueuedToolEventArgs> action = renderer_MouseDown;
                    this.Invoke(action, new object[] { sender, e });
                }
                else
                {
                    UpdateHistoryButtons(true);
                    ok.Enabled = false;
                }
            }
            catch (ObjectDisposedException) { }
        }

        private void renderer_Invalidated(object sender, InvalidateEventArgs e)
        {
            mesh.Render(surface, EffectSourceSurface, e.InvalidRect, ColorBgra.Red);
            canvas.InvalidateCanvas(e.InvalidRect);
        }

        private void toolRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton button = sender as RadioButton;
            if (button.Checked)
            {
                if (rendermodes.ContainsKey(button))
                    mode = rendermodes[button];
            }
        }

        private void brushSize_Validating(object sender, EventArgs e)
        {
            float penSize;
            bool valid = float.TryParse(this.brushSize.Text, out penSize);

            if (!valid)
            {
                this.brushSize.BackColor = Color.Red;
            }
            else
            {
                if (penSize < minPenSize)
                {
                    this.brushSize.BackColor = Color.Red;
                }
                else if (penSize > maxPenSize)
                {
                    this.brushSize.BackColor = Color.Red;
                }
                else
                {
                    // Clear the error, if any
                    this.brushSize.BackColor = SystemColors.Window;
                    this.brushSize.ToolTipText = string.Empty;
                    OnPenChanged();
                }
            }
        }

        private void OnPenChanged()
        {
            canvas.BrushSize = BrushSize;
        }

        private void donate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Services.GetService<PaintDotNet.AppModel.IShellService>().LaunchUrl(this, "http://forums.getpaint.net/index.php?showtopic=7291");
        }

        private void ConfigDialog_Load(object sender, EventArgs e)
        {
            surface = new Surface(EffectSourceSurface.Size);
            canvas.Surface = surface;
            canvas.Selection = Selection;
            mesh = new DisplacementMesh(EffectSourceSurface.Size);
            mesh.Render(surface, EffectSourceSurface, EffectSourceSurface.Bounds);
            historystack = new HistoryStack(mesh, false);

            InitializeRenderer();

            this.DesktopLocation = Owner.PointToScreen(new Point(0, 30));
            this.Size = new Size(Owner.ClientSize.Width, Owner.ClientSize.Height - 30);
            this.WindowState = Owner.WindowState;
        }

        private void brushSizeDecrement_Click(object sender, EventArgs e)
        {
            int amount = -1;

            if ((Control.ModifierKeys & Keys.Control) != 0)
            {
                amount *= 5;
            }

            AddToPenSize(amount);
        }

        private void brushSizeIncrement_Click(object sender, EventArgs e)
        {
            int amount = 1;

            if ((Control.ModifierKeys & Keys.Control) != 0)
            {
                amount *= 5;
            }

            AddToPenSize(amount);
        }

        public void AddToPenSize(int delta)
        {
            int newWidth = Int32Util.Clamp(BrushSize + delta, minPenSize, maxPenSize);
            BrushSize = newWidth;
        }

        protected override void InitialInitToken()
        {
            theEffectToken = new ConfigToken();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            canvas.PerformMouseWheel(e);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (!e.Handled) //hasn't been handled? our turn, then.
            {
                if (e.KeyChar == decPenSizeShortcut)
                {
                    AddToPenSize(-1);
                    e.Handled = true;
                }
                else if (e.KeyChar == decPenSizeBy5Shortcut && (ModifierKeys & Keys.Control) != 0)
                {
                    AddToPenSize(-5);
                    e.Handled = true;
                }
                else if (e.KeyChar == incPenSizeShortcut)
                {
                    AddToPenSize(+1);
                    e.Handled = true;
                }
                else if (e.KeyChar == incPenSizeBy5Shortcut && (ModifierKeys & Keys.Control) != 0)
                {
                    AddToPenSize(+5);
                    e.Handled = true;
                }
                else if (e.KeyChar == undoShortcut && (ModifierKeys & Keys.Control) != 0)
                {
                    DoUndo();
                }
                else if (e.KeyChar == redoShortcut && (ModifierKeys & Keys.Control) != 0)
                {
                    DoRedo();
                }
            }
            base.OnKeyPress(e);
        }

        private void DoUndo()
        {
            if (historystack.CanStepBack)
            {
                historystack.StepBack(mesh);
                UpdateHistoryButtons(false);
                mesh.Render(surface, EffectSourceSurface, EffectSourceSurface.Bounds, ColorBgra.Red);
                canvas.Invalidate();
            }
        }

        private void DoRedo()
        {
            if (historystack.CanStepForward)
            {
                historystack.StepForward(mesh);
                UpdateHistoryButtons(false);
                mesh.Render(surface, EffectSourceSurface, EffectSourceSurface.Bounds, ColorBgra.Red);
                canvas.Invalidate();
            }
        }

        private void UpdateHistoryButtons(bool disable)
        {
            if (!this.IsDisposed)
            {
                if (this.InvokeRequired)
                {
                    Action<bool> uhbd = UpdateHistoryButtons;
                    try
                    {
                        this.Invoke(uhbd, new object[] { disable });
                    }
                    catch { }
                }
                else
                {
                    redo.Enabled = historystack.CanStepForward && !disable;
                    undo.Enabled = historystack.CanStepBack && !disable;
                }
            }
        }

        public int BrushSize
        {
            get
            {
                int width;

                try
                {
                    width = (int)float.Parse(this.brushSize.Text);
                }

                catch (FormatException)
                {
                    width = 30;
                }

                return width;
            }
            set
            {
                this.brushSize.Text = value.ToString();
                OnPenChanged();
            }
        }

        public float Pressure
        {
            get
            {
                return this.pressure.Value;
            }
            set
            {
                this.pressure.Value = value;
            }
        }

        public float Density
        {
            get
            {
                return this.density.Value;
            }
            set
            {
                this.density.Value = value;
            }
        }

        private void canvas_CanvasMouseHold(object sender, CanvasMouseEventArgs e)
        {
            renderer.AddEvent(
                new LiquifyEventArgs(
                    QueuedToolEventType.MouseHold,
                    e.Button,
                    (int)e.X,
                    (int)e.Y,
                    BrushSize,
                    Pressure,
                    Density,
                    mode));
        }

        private void canvas_CanvasMouseDown(object sender, CanvasMouseEventArgs e)
        {
            renderer.AddEvent(
                new LiquifyEventArgs(
                    QueuedToolEventType.MouseDown,
                    e.Button,
                    (int)e.X,
                    (int)e.Y,
                    BrushSize,
                    Pressure,
                    Density,
                    mode));
        }

        private void canvas_CanvasMouseMove(object sender, CanvasMouseEventArgs e)
        {
            renderer.AddEvent(
                new LiquifyEventArgs(
                    QueuedToolEventType.MouseMove,
                    e.Button,
                    (int)e.X,
                    (int)e.Y,
                    BrushSize,
                    Pressure,
                    Density,
                    mode));
        }

        private void canvas_CanvasMouseUp(object sender, CanvasMouseEventArgs e)
        {
            renderer.AddEvent(
                new LiquifyEventArgs(
                    QueuedToolEventType.MouseUp,
                    e.Button,
                    (int)e.X,
                    (int)e.Y,
                    BrushSize,
                    Pressure,
                    Density,
                    mode));
        }


        private void zoom_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (zoom.SelectedIndex >= 0)
                canvas.ZoomFactor = CanvasPanel.ZoomFactors[zoom.SelectedIndex];
        }


        protected override void InitDialogFromToken(EffectConfigToken effectTokenCopy)
        {
            ConfigToken token = effectTokenCopy as ConfigToken;
            Pressure = token.pressure;
            Density = token.density;
            BrushSize = token.size;
        }

        protected override void InitTokenFromDialog()
        {
            ConfigToken token = EffectToken as ConfigToken;
            token.pressure = Pressure;
            token.density = Density;
            token.size = BrushSize;
            token.mesh = mesh;
        }

        private void ok_Click(object sender, EventArgs e)
        {
            FinishTokenUpdate();
        }

        const string dialogFilter = "Liquify Mesh (*.MSH)|*.msh";
        private void load_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();

            ofd.Filter = dialogFilter;
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    FileStream fs = new FileStream(ofd.FileName, FileMode.Open);
                    mesh.Load(fs);
                    mesh.Render(surface, EffectSourceSurface, surface.Bounds);
                    canvas.InvalidateCanvas();
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this,
                        "Error loading mesh from file:\n\n" + exception.ToString(),
                        "Error loading mesh file",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void save_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = dialogFilter;
            sfd.FileName = "Liquify.msh";
            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try {
                    FileStream fs = new FileStream(sfd.FileName, FileMode.Create);
                    mesh.Save(fs);
                }
                catch(Exception exception)
                {
                    MessageBox.Show(this,
                        "Error save mesh to file:\n\n" + exception.ToString(),
                        "Error saving  mesh to file",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void undo_Click(object sender, EventArgs e)
        {
            DoUndo();
        }

        private void redo_Click(object sender, EventArgs e)
        {
            DoRedo();
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            renderer.Abort();
        }

        private void zoomOut_Click(object sender, EventArgs e)
        {
            canvas.ZoomOut();
        }

        private void zoomIn_Click(object sender, EventArgs e)
        {
            canvas.ZoomIn();
        }
    }
}
