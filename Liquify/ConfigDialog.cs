using PaintDotNet;
using PaintDotNet.Effects;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using pyrochild.effects.common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace pyrochild.effects.liquify
{
    public partial class ConfigDialog : EffectConfigForm<Liquify, ConfigToken>
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
        private Surface source;
        private SliderControl pressure, density;
        private const int minPenSize = 2;
        private const int maxPenSize = 1500;
        private int lastValidBrushSize = 50;
        private int[] brushSizes =
            { 
                2, 3, 4, 5, 6, 7, 8, 9, 10, 
                11, 12, 13, 14, 15, 20, 25, 30, 
                35, 40, 45, 50, 60, 70, 80, 90, 100, 125, 150, 200, 300
            };
        private DisplacementMesh mesh;
        private Dictionary<Control, LiquifyMode> rendermodes;
        private LiquifyMode mode;

        // true from the moment a mouse-down is queued until the renderer has finished that stroke.
        // The render thread owns the mesh for that time, so undo, redo, load and save have to wait.
        private bool strokePending;

        // set once the dialog is closing; the render thread's callbacks check it and back off
        private volatile bool closing;

        private float DpiScale
        {
            get { return this.DeviceDpi / 96f; }
        }

        // RenderScans is the selection's actual shape; RenderBounds is only its bounding box.
        private PdnRegion CreateSelectionRegion()
        {
            Rectangle[] scans = Environment.Selection.RenderScans
                .Select(r => new Rectangle(r.X, r.Y, r.Width, r.Height))
                .ToArray();

            using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath(System.Drawing.Drawing2D.FillMode.Winding))
            {
                if (scans.Length > 0)
                {
                    path.AddRectangles(scans);
                }
                return new PdnRegion(path);
            }
        }

        public ConfigDialog()
        {
            InitializeComponent();
            this.Load += (themeSender, themeArgs) => ThemeHelper.Apply(this);
            this.Shown += (themeSender, themeArgs) => ThemeHelper.Apply(this);

            this.Text = Liquify.StaticDialogName;

            float dpiScale = DpiScale;

            pressure = new SliderControl();
            pressure.Minimum = .01f;
            density = new SliderControl();

            if (dpiScale > 1.01f)
            {
                // settingStrip's own button icons aren't scaled here: ToolStrip does that itself.
                pressure.Size = new Size((int)Math.Round(pressure.Width * dpiScale), (int)Math.Round(pressure.Height * dpiScale));
                density.Size = new Size((int)Math.Round(density.Width * dpiScale), (int)Math.Round(density.Height * dpiScale));

                brushSize.Size = new Size((int)Math.Round(brushSize.Width * dpiScale), brushSize.Height);
                zoom.Size = new Size((int)Math.Round(zoom.Width * dpiScale), zoom.Height);
            }

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
            float dpiScale = DpiScale;

            push.Image = LoadIcon(t, "images.push.png", dpiScale);
            reconstruct.Image = LoadIcon(t, "images.reconstruct.png", dpiScale);
            bloat.Image = LoadIcon(t, "images.bloat.png", dpiScale);
            pucker.Image = LoadIcon(t, "images.pucker.png", dpiScale);
            load.Image = LoadIcon(t, "images.open.png", dpiScale);
            save.Image = LoadIcon(t, "images.save.png", dpiScale);
            twistleft.Image = LoadIcon(t, "images.twistleft.png", dpiScale);
            twistright.Image = LoadIcon(t, "images.twistright.png", dpiScale);

            // ToolStrip scales these itself, so pass the unscaled bitmap.
            brushSizeIncrement.Image = new Bitmap(t, "images.plus.png");
            brushSizeDecrement.Image = new Bitmap(t, "images.minus.png");
            undo.Image = new Bitmap(t, "images.undo.png");
            redo.Image = new Bitmap(t, "images.redo.png");
            zoomIn.Image = new Bitmap(t, "images.zoomin.png");
            zoomOut.Image = new Bitmap(t, "images.zoomout.png");

            freeze.Image = LoadIcon(t, "images.freeze.png", dpiScale);
            thaw.Image = LoadIcon(t, "images.thaw.png", dpiScale);
        }

        private static Bitmap LoadIcon(Type resourceType, string resourceName, float dpiScale)
        {
            Bitmap original = new Bitmap(resourceType, resourceName);

            if (dpiScale <= 1.01f)
            {
                return original;
            }

            int width = Math.Max(1, (int)Math.Round(original.Width * dpiScale));
            int height = Math.Max(1, (int)Math.Round(original.Height * dpiScale));

            Bitmap scaled = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(original, 0, 0, width, height);
            }
            original.Dispose();

            return scaled;
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
            // Runs on the render thread. The history item has to be captured before the renderer touches
            // the mesh again, so wait for the UI thread to do it, but give up if the dialog closes meanwhile:
            // the UI thread is then waiting for this thread to stop.
            if (closing)
            {
                return;
            }

            ManualResetEventSlim done = new ManualResetEventSlim(false);
            try
            {
                this.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (!closing)
                        {
                            historystack.AddHistoryItem(mesh, renderer.PopTotalInvalidRect());
                            SetStrokePending(false);
                        }
                    }
                    finally
                    {
                        done.Set();
                    }
                }));
            }
            catch (InvalidOperationException)
            {
                // the window is already gone
                return;
            }

            while (!done.Wait(50))
            {
                if (closing)
                {
                    return;
                }
            }
        }

        private void SetStrokePending(bool pending)
        {
            strokePending = pending;
            UpdateHistoryButtons();
            ok.Enabled = !pending;
            load.Enabled = !pending;
            save.Enabled = !pending;
        }

        // Stops the render thread and waits for it, so nothing is still drawing when the surfaces go away.
        private void StopRenderer()
        {
            closing = true;
            if (renderer != null)
            {
                renderer.Dispose();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopRenderer();
            base.OnFormClosed(e);
        }

        private void renderer_Invalidated(object sender, InvalidateEventArgs e)
        {
            if (closing)
            {
                return;
            }

            mesh.Render(surface, source, e.InvalidRect, ColorBgra.Red);
            canvas.InvalidateCanvas(e.InvalidRect);
        }

        private unsafe Surface GetSourceAsClassicSurface()
        {
            SizeInt32 docSize = Environment.Document.Size;
            Surface result = new Surface(docSize.Width, docSize.Height);

            using (IEffectInputBitmap<ColorBgra32> srcBitmap = Environment.GetSourceBitmapBgra32())
            using (IBitmapLock<ColorBgra32> srcLock = srcBitmap.Lock(new RectInt32(0, 0, docSize.Width, docSize.Height)))
            {
                RegionPtr<ColorBgra32> srcRegion32 = new RegionPtr<ColorBgra32>(srcLock.Buffer, srcLock.Size, srcLock.BufferStride);
                RegionPtr<ColorBgra> dstRegion = new RegionPtr<ColorBgra>(result.GetPointPointer(0, 0), result.Width, result.Height, result.Stride);
                srcRegion32.Cast<ColorBgra>().CopyTo(dstRegion);
            }

            return result;
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

            if (TryParseBrushSize(out penSize))
            {
                // Clear the error, if any
                this.brushSize.BackColor = ThemeHelper.FieldBackColor;
                this.brushSize.ToolTipText = string.Empty;
                lastValidBrushSize = (int)penSize;
                OnPenChanged();
            }
            else
            {
                this.brushSize.BackColor = Color.Red;
            }
        }

        private bool TryParseBrushSize(out float penSize)
        {
            return float.TryParse(this.brushSize.Text, out penSize)
                && penSize >= minPenSize
                && penSize <= maxPenSize;
        }

        private void brushSize_LostFocus(object sender, EventArgs e)
        {
            ResetInvalidBrushSize();
        }

        // Puts the last good size back if the box holds something that isn't one.
        private void ResetInvalidBrushSize()
        {
            float penSize;
            if (!TryParseBrushSize(out penSize))
            {
                BrushSize = lastValidBrushSize;
            }
        }

        private void OnPenChanged()
        {
            canvas.BrushSize = BrushSize;
        }

        private void donate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ((PaintDotNet.AppModel.IShellService)Services.GetService(typeof(PaintDotNet.AppModel.IShellService))).LaunchUrl(this, "https://forums.getpaint.net/index.php?showtopic=7291");
        }

        private void ConfigDialog_Load(object sender, EventArgs e)
        {
            int topMargin = (int)Math.Round(6 * DpiScale);
            settingStrip.Location = new Point(settingStrip.Location.X, settingStrip.Location.Y + topMargin);

            int newCanvasTop = settingStrip.Bottom;
            int topDelta = newCanvasTop - canvas.Top;
            canvas.Bounds = new Rectangle(canvas.Left, newCanvasTop, canvas.Width, canvas.Height - topDelta);

            source = GetSourceAsClassicSurface();
            surface = new Surface(source.Size);
            canvas.Surface = surface;

            canvas.Selection = CreateSelectionRegion();

            mesh = new DisplacementMesh(source.Size);
            mesh.Render(surface, source, source.Bounds);
            historystack = new HistoryStack(mesh);

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
            int newWidth = Math.Clamp(BrushSize + delta, minPenSize, maxPenSize);
            BrushSize = newWidth;
        }

        protected override EffectConfigToken OnCreateInitialToken()
        {
            return new ConfigToken();
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
            if (historystack.CanStepBack && !strokePending)
            {
                Rectangle rect = historystack.StepBack(mesh);
                UpdateHistoryButtons();
                mesh.Render(surface, source, rect, ColorBgra.Red);
                canvas.InvalidateCanvas(rect);
            }
        }

        private void DoRedo()
        {
            if (historystack.CanStepForward && !strokePending)
            {
                Rectangle rect = historystack.StepForward(mesh);
                UpdateHistoryButtons();
                mesh.Render(surface, source, rect, ColorBgra.Red);
                canvas.InvalidateCanvas(rect);
            }
        }

        private void UpdateHistoryButtons()
        {
            redo.Enabled = historystack.CanStepForward && !strokePending;
            undo.Enabled = historystack.CanStepBack && !strokePending;
        }

        public int BrushSize
        {
            get
            {
                float width;

                if (!float.TryParse(this.brushSize.Text, out width) || float.IsNaN(width))
                {
                    width = 30;
                }

                // the text box only flags out-of-range input, so clamp here: the renderer can't handle sizes below 2
                return (int)Math.Clamp(width, minPenSize, maxPenSize);
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
            // clicking the canvas doesn't take focus away from the size box, so check it here too
            ResetInvalidBrushSize();

            SetStrokePending(true);
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


        protected override void OnUpdateDialogFromToken(ConfigToken token)
        {
            Pressure = token.pressure;
            Density = token.density;
            BrushSize = token.size;
        }

        protected override void OnUpdateTokenFromDialog(ConfigToken token)
        {
            token.pressure = Pressure;
            token.density = Density;
            token.size = BrushSize;
            token.mesh = mesh;
        }

        private void ok_Click(object sender, EventArgs e)
        {
            UpdateTokenFromDialog();
        }

        const string dialogFilter = "Liquify Mesh (*.MSH)|*.msh";
        private void load_Click(object sender, EventArgs e)
        {
            if (strokePending)
            {
                return;
            }

            OpenFileDialog ofd = new OpenFileDialog();

            ofd.Filter = dialogFilter;
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    FileStream fs = new FileStream(ofd.FileName, FileMode.Open);
                    mesh.Load(fs);
                    mesh.Render(surface, source, surface.Bounds, ColorBgra.Red);
                    canvas.InvalidateCanvas();

                    // a loaded mesh is an edit like any other, so it can be undone
                    historystack.AddHistoryItem(mesh, mesh.Bounds);
                    UpdateHistoryButtons();
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
            if (strokePending)
            {
                return;
            }

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
