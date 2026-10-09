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
        private BitmapSurface source;
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

        // True from the moment a mouse-down is queued until the renderer has finished that stroke.
        // The render thread owns the mesh for that time, so undo, redo, load and save have to wait.
        // A count, because a second stroke can be queued before the renderer has finished the first.
        private int strokesPending;
        private bool strokePending
        {
            get { return strokesPending > 0; }
        }

        // set on the render thread when undo couldn't keep what a stroke was about to overwrite
        private volatile bool strokeRanOutOfMemory;

        // The controls that can't be used during a stroke only look disabled once it has gone on
        // for a moment; greying them out and back for every short stroke reads as flicker.
        private bool controlsLocked;
        private System.Windows.Forms.Timer lockTimer;

        // OK was pressed while the renderer was still finishing a stroke: close when it has
        private bool okWhenStrokeEnds;

        // set once the dialog is closing; the render thread's callbacks check it and back off
        private volatile bool closing;

        // true from Load until the dialog has been shown; see the constructor
        private bool fitZoomPending;

        // false until the dialog is on screen, so that setting up the brush doesn't flash it
        private bool brushPreviewEnabled;

        // distance between the mesh grid's lines, in image pixels; 0 means the grid is off
        private float gridSpacing;

        // true while the "show original" key is held: the canvas shows the source instead of the preview
        private bool comparing;

        // true while the "all layers" button or key is held: the canvas shows the whole document
        private bool compositing;

        private float DpiScale
        {
            get { return this.DeviceDpi / 96f; }
        }

        // RenderScans is the selection's actual shape; RenderBounds is only its bounding box.
        private PdnRegion CreateSelectionRegion()
        {
            Rectangle[] scans;
            try
            {
                scans = Environment.Selection.RenderScans
                    .Select(r => new Rectangle(r.X, r.Y, r.Width, r.Height))
                    .ToArray();
            }
            catch (InvalidOperationException)
            {
                // Something other than Paint.NET's own menu opened the dialog (the Plugin Browser
                // plugin does), and the selection hasn't been set up. Left alone, this took
                // Paint.NET down. Show the whole image as selected.
                scans = new Rectangle[] { source.Bounds };
            }

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

            this.Load += (statusSender, statusArgs) => LayoutStatusLine();

            // The window gets its final size (and may be maximized) after Load, so keep fitting the
            // image as the canvas resizes until the dialog has been shown. That way the first thing
            // drawn is already at the right zoom.
            canvas.Resize += (resizeSender, resizeArgs) =>
            {
                if (fitZoomPending)
                {
                    canvas.ZoomToFit();
                }
            };
            this.Shown += (shownSender, shownArgs) =>
            {
                canvas.ZoomToFit();
                fitZoomPending = false;
                brushPreviewEnabled = true;
            };

            this.Text = Liquify.StaticDialogName;

            float dpiScale = DpiScale;

            pressure = new SliderControl();
            pressure.Minimum = .01f;
            density = new SliderControl();
            density.ValueChanged += (densitySender, densityArgs) => UpdateBrushInnerRing();

            lockTimer = new System.Windows.Forms.Timer(components);
            lockTimer.Interval = 250;
            lockTimer.Tick += (timerSender, timerArgs) =>
            {
                lockTimer.Stop();
                if (strokePending)
                {
                    ShowControlsLocked(true);
                }
            };

            // held down like the O key; leaving the button ends it too, in case the release goes elsewhere
            original.MouseDown += (originalSender, originalArgs) => SetComparing(true);
            original.MouseUp += (originalSender, originalArgs) => SetComparing(false);
            original.MouseLeave += (originalSender, originalArgs) => SetComparing(false);
            allLayers.MouseDown += (allSender, allArgs) => SetCompositing(true);
            allLayers.MouseUp += (allSender, allArgs) => SetCompositing(false);
            allLayers.MouseLeave += (allSender, allArgs) => SetCompositing(false);

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
            // the last entry is "fit"; its text gains the percentage while it is the zoom in use
            this.zoom.Items.Add(fitZoomText);
            this.zoom.ComboBox.ResumeLayout(false);
            this.zoom.Text = percent100;

            foreach (Control control in toolPanel.Controls)
            {
                RadioButton radioButton = control as RadioButton;
                if (radioButton != null)
                {
                    radioButton.CheckedChanged += new EventHandler(toolRadioButton_CheckedChanged);
                }
            }

            AddBackgroundMenus();
            AddUpdateItems();
            InitializeUIImages();
            InitializeTooltips();
            UpdateStatus();
            UpdateBrushInnerRing();

            // Paint.NET has already given the form its theme colors by now (in the base constructor),
            // so style the controls here too. Waiting for Load lets them show up light first.
            ThemeHelper.Apply(this);
        }

        // The background choices are also on the canvas's right-click menu, where few people look.
        private void AddBackgroundMenus()
        {
            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            viewMenu.DropDownItems.Add(CreateBackgroundMenu("Canvas background", true));
            viewMenu.DropDownItems.Add(CreateBackgroundMenu("Around the canvas", false));

            // tooltips on a menu get in the way of the entries and submenus next to them
            viewMenu.DropDown.ShowItemToolTips = false;
        }

        private ToolStripMenuItem CreateBackgroundMenu(string text, bool onCanvas)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);

            // filled each time it opens; the placeholder is what makes it a submenu until then
            item.DropDownItems.Add(new ToolStripMenuItem("Transparent"));
            item.DropDownOpening += (sender, e) => canvas.FillBackgroundMenu(item.DropDown, onCanvas);
            return item;
        }

        private const string repository = "bsneeze/pdn-liquify";

        // a beta is told about newer betas as well
#if BETA
        private const string betaRepository = repository;
#else
        private const string betaRepository = null;
#endif

        // An update is a plugin pack that carries a newer Liquify; a release here that isn't in
        // a pack yet is not one. What is new in it is on the page of this repository's release of
        // that version. Static, so that the check is made once for as long as Paint.NET runs.
        private static readonly UpdateChecker updates = new UpdateChecker(
            "Liquify", typeof(Liquify).Assembly.GetName().Version,
            "bsneeze/pdn-pyrochild-plugin-pack", "Liquify.version.txt", betaRepository,
            version => "https://github.com/" + repository + "/releases/tag/v" + FormatVersion(version));

        // the last part is ddHH, written with its leading zero in tags and everywhere else
        private static string FormatVersion(Version version)
        {
            return string.Format("{0}.{1:0000}", version.ToString(3), version.Revision);
        }

        private CheckBox checkForUpdates;
        private UpdateBanner updateBanner;
        private bool updateAvailable;
        private AvailableUpdate update;

        private LinkLabel help;

        // Help is the "Using it" part of the README, which says what the tooltips have no room for.
        private void ShowHelp()
        {
            LaunchUrl("https://github.com/" + repository + "#using-it");
        }

        // F1. Windows turns the key into a help request of its own, whatever is done with the key
        // press, and passes it up from the focused control to this form and then to the form's
        // owner. Answering it here is what stops Paint.NET's own help opening as well.
        protected override void OnHelpRequested(HelpEventArgs hevent)
        {
            ShowHelp();
            hevent.Handled = true;
        }

        // The help link and the update setting, which go after the donate link, and a banner
        // above the canvas that only shows when there is a newer release.
        private void AddUpdateItems()
        {
            help = new LinkLabel();
            help.Text = "Help";
            help.AutoSize = true;
            help.LinkClicked += (sender, e) => ShowHelp();
            tooltip.SetToolTip(help, "How to use Liquify (F1)");
            panel1.Controls.Add(help);

            checkForUpdates = new CheckBox();
            checkForUpdates.Text = "Check for updates";
            checkForUpdates.AutoSize = true;
            checkForUpdates.Checked = updates.Enabled;
            checkForUpdates.CheckedChanged += (sender, e) =>
            {
                updates.Enabled = checkForUpdates.Checked;
                ShowUpdateIfNewer();
            };
            tooltip.SetToolTip(checkForUpdates, "Look for a newer version on GitHub when Liquify opens");

            panel1.Controls.Add(checkForUpdates);

            updateBanner = new UpdateBanner();
            updateBanner.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            updateBanner.Visible = false;
            updateBanner.GetClicked += (sender, e) => LaunchUrl(update.PageUrl);
            updateBanner.WhatsNewClicked += (sender, e) => LaunchUrl(update.NotesUrl);
            updateBanner.NotNowClicked += (sender, e) =>
            {
                updates.Snooze();
                SetUpdateAvailable(false);
            };
            // closed for now: it is back the next time the dialog opens
            updateBanner.CloseClicked += (sender, e) => SetUpdateAvailable(false);
            this.Controls.Add(updateBanner);

            this.Shown += (sender, e) => ShowUpdateIfNewer();
        }

        private async void ShowUpdateIfNewer()
        {
            SetUpdateAvailable(false);
            if (!checkForUpdates.Checked)
            {
                return;
            }

            AvailableUpdate found = await updates.FindNewerAsync();

            if (found != null && !IsDisposed && checkForUpdates.Checked && !updates.Snoozed)
            {
                update = found;

                updateBanner.Message = string.Format("A new version of Liquify is available! ({0})", FormatVersion(found.Version));
                updateBanner.ShowWhatsNew = found.NotesUrl != null;
                SetUpdateAvailable(true);
            }
        }

        private int bannerHeight;

        // The banner takes its room from the top of the canvas, and gives it back.
        private void SetUpdateAvailable(bool available)
        {
            if (updateAvailable == available)
            {
                return;
            }
            updateAvailable = available;

            int top = canvas.Top - bannerHeight;
            bannerHeight = available ? updateBanner.PreferredBannerHeight : 0;

            updateBanner.SetBounds(canvas.Left, top, canvas.Width, bannerHeight);
            canvas.SetBounds(canvas.Left, top + bannerHeight, canvas.Width, canvas.Bottom - (top + bannerHeight));
            updateBanner.Visible = available;
        }

        // The donate link, the help link and the update setting, then the status line in the space
        // left before the buttons, wherever the font and the DPI have put them.
        private void LayoutStatusLine()
        {
            int gap = (int)Math.Round(10 * DpiScale);
            int middle = donate.Top + donate.Height / 2;

            help.Location = new Point(donate.Right + gap, donate.Top);
            checkForUpdates.Location = new Point(help.Right + gap, middle - checkForUpdates.Height / 2);
            int right = checkForUpdates.Right;

            status.SetBounds(right + gap, 0, Math.Max(0, ok.Left - right - 2 * gap), 0,
                BoundsSpecified.X | BoundsSpecified.Width);
        }

        private void LaunchUrl(string url)
        {
            ((PaintDotNet.AppModel.IShellService)Services.GetService(typeof(PaintDotNet.AppModel.IShellService))).LaunchUrl(this, url);
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
            resetAll.Image = LoadIcon(t, "images.resetall.png", dpiScale);
            clearMask.Image = LoadIcon(t, "images.clearmask.png", dpiScale);
            invertMask.Image = LoadIcon(t, "images.invertmask.png", dpiScale);
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
            // the letters are the tool shortcuts handled in ProcessCmdKey
            tooltip.SetToolTip(push, "Push (P)");
            tooltip.SetToolTip(reconstruct, "Reconstruct (E)");
            tooltip.SetToolTip(resetAll, "Reset all distortion");
            tooltip.SetToolTip(bloat, "Bloat (B)");
            tooltip.SetToolTip(pucker, "Pucker (S)");
            tooltip.SetToolTip(twistleft, "Twist left (L)");
            tooltip.SetToolTip(twistright, "Twist right (R)");
            tooltip.SetToolTip(save, "Save mesh (.msh, the format Photoshop's Liquify uses)");
            tooltip.SetToolTip(load, "Load mesh (.msh, including ones saved by Photoshop's Liquify)");
            tooltip.SetToolTip(freeze, "Freeze (F)");
            tooltip.SetToolTip(thaw, "Thaw (T)");
            tooltip.SetToolTip(clearMask, "Thaw everything");
            tooltip.SetToolTip(invertMask, "Invert frozen area");
            viewMenu.AutoToolTip = false;
            original.ToolTipText = "Hold to see the original image (O)";
            allLayers.ToolTipText = "Hold to see every visible layer of the document, with this layer's changes and without the tint or the grid (A)";
            undo.ToolTipText = "Undo (Ctrl+Z)";
            redo.ToolTipText = "Redo (Ctrl+Y or Ctrl+Shift+Z)";
            brushSizeIncrement.ToolTipText = "Increase brush size (], or Ctrl+] for 5)";
            brushSizeDecrement.ToolTipText = "Decrease brush size ([, or Ctrl+[ for 5)";

            const string pressureTip = "How strongly the brush acts";
            const string densityTip = "How much of the brush acts at full strength. Low fades out from the center; high is even to the edge. The inner ring on the brush shows where it is down to half.";
            pressureLabel.ToolTipText = pressureTip;
            densityLabel.ToolTipText = densityTip;
            tooltip.SetToolTip(pressure, pressureTip);
            tooltip.SetToolTip(density, densityTip);
            zoomIn.ToolTipText = "Zoom in (Ctrl +)";
            zoomOut.ToolTipText = "Zoom out (Ctrl -)";
            zoom.ToolTipText = "Zoom (Ctrl+0 for 100%, Ctrl+B to fit)";
        }

        private void InitializeRenderer()
        {
            renderer = new LiquifyRenderer(mesh);
            renderer.BeforeMeshChange = rect =>
            {
                if (historystack.BeforeChange(mesh, rect))
                {
                    return true;
                }

                // The renderer now stops the stroke. End it on this side too, straight away and
                // not when the button is let go; the one message about it comes when it has ended
                // and it is known whether what it did could be recorded.
                strokeRanOutOfMemory = true;
                try
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        if (!closing)
                        {
                            canvas.EndMouseDrag();
                        }
                    }));
                }
                catch (InvalidOperationException)
                {
                    // the window is already gone
                }
                return false;
            };

            renderer.Invalidated += new InvalidateEventHandler(renderer_Invalidated);
            renderer.MouseUp += new QueuedToolEventHandler(renderer_MouseUp);
            renderer.Error += renderer_Error;

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

        private const string fitZoomText = "Fit";
        private bool updatingZoomBox;

        private void canvas_ZoomFactorChanged(object sender, EventArgs e)
        {
            int fitIndex = CanvasPanel.ZoomFactors.Length;

            // changing the box's entries and selection here must not come back as a zoom request
            updatingZoomBox = true;
            try
            {
                if (canvas.ZoomedToFit)
                {
                    zoom.Items[fitIndex] = string.Format("{0} ({1:0}%)", fitZoomText, canvas.ZoomFactor * 100);
                    zoom.SelectedIndex = fitIndex;
                }
                else
                {
                    zoom.SelectedIndex = Array.IndexOf(CanvasPanel.ZoomFactors, canvas.ZoomFactor);
                    if (!fitZoomText.Equals(zoom.Items[fitIndex]))
                    {
                        zoom.Items[fitIndex] = fitZoomText;
                    }
                }
            }
            finally
            {
                updatingZoomBox = false;
            }

            UpdateGrid();
        }

        // Everything that draws the preview goes through here, so the mask tint stays consistent.
        private void RenderPreview(Rectangle rect)
        {
            // a fully transparent mask color leaves the image untinted
            ColorBgra maskColor = (showMask.Checked && !compositing) ? ColorBgra.Red : ColorBgra.Red.NewAlpha(0);
            mesh.Render(surface, source, rect, maskColor);
        }

        private void RenderWholePreview()
        {
            RenderPreview(source.Bounds);
            canvas.InvalidateCanvas();
        }

        // The grid isn't part of the preview image: the canvas calls DrawGridRow as it paints, so the
        // lines are drawn at screen resolution and stay one pixel wide at any zoom.
        private void UpdateGrid()
        {
            float screenSpacing = meshSmall.Checked ? 16 : meshLarge.Checked ? 64 : 0;
            screenSpacing *= DpiScale;

            // the same spacing on screen at any zoom
            gridSpacing = screenSpacing / canvas.ZoomFactor;

            if (gridSpacing > 0 && canvas.RowOverlay == null)
            {
                canvas.RowOverlay = DrawGridRow;
            }
            else if (gridSpacing == 0 && canvas.RowOverlay != null)
            {
                canvas.RowOverlay = null;
            }
            else
            {
                canvas.InvalidateCanvas();
            }
        }

        // Called by the canvas from several threads while it paints.
        private void DrawGridRow(IntPtr pixels, int canvasX, int canvasY, int count, float scale)
        {
            float spacing = gridSpacing;

            if (spacing > 0 && !comparing && !compositing && mesh != null)
            {
                mesh.DrawGridRow(pixels, canvasX, canvasY, count, scale, spacing, 0.5f / scale);
            }
        }

        private void showMask_Click(object sender, EventArgs e)
        {
            if (mesh != null)
            {
                RenderWholePreview();
            }
        }

        // While the key is held the canvas shows the untouched source image.
        private void SetComparing(bool compare)
        {
            if (comparing == compare || source == null || compositing)
            {
                return;
            }

            comparing = compare;
            canvas.Surface = compare ? (ISurface<ColorBgra>)source : surface;
            UpdateStatus();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.O)
            {
                SetComparing(false);
            }
            else if (e.KeyCode == Keys.A)
            {
                SetCompositing(false);
            }

            base.OnKeyUp(e);
        }

        // While its button is held the canvas shows the document as it will look: the layers beneath,
        // this layer with its own blend mode and opacity, and the layers above. The canvas's image
        // becomes the layers beneath, and the preview is drawn over it as the first foreground layer.
        // Made on first use and kept, so later presses are instant. Null while the "all layers
        // beneath" background is in use: that is the same picture, and is shown instead.
        private Surface layersBeneathSurface;
        private bool allLayersShown; // "all layers" has been used, so the picture is worth keeping

        private void SetCompositing(bool composite)
        {
            if (compositing == composite || source == null || (composite && comparing))
            {
                return;
            }

            compositing = composite;

            if (composite)
            {
                allLayersShown = true;

                Cursor previous = Cursor.Current;
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    int index = Environment.SourceLayerIndex;
                    IEffectLayerInfo layer = Environment.Document.Layers[index];

                    // the "all layers beneath" background is the same picture, if it is in use
                    Surface beneath = (layerBackground != null && layerBackgroundKind == CanvasBackground.AllLayersBeneath)
                        ? layerBackground
                        : layersBeneathSurface;
                    if (beneath == null)
                    {
                        beneath = layersBeneathSurface = LayerCompositor.Render(Environment.Document, 0, index, true);
                    }

                    List<CanvasForegroundLayer> layers = new List<CanvasForegroundLayer>();
                    layers.Add(new CanvasForegroundLayer(surface, LayerCompositor.CreateOp(layer.BlendMode, layer.Opacity)));
                    if (HasLayersAbove)
                    {
                        EnsureLayersAbove();
                        layers.AddRange(layersAboveLayers);
                    }

                    if (showMask.Checked)
                    {
                        RenderPreview(source.Bounds); // without the tint
                    }

                    canvas.BackgroundSurfaceHidden = true;
                    canvas.ForegroundLayers = layers.ToArray();
                    canvas.Surface = beneath;
                }
                finally
                {
                    Cursor.Current = previous;
                }
            }
            else
            {
                if (showMask.Checked)
                {
                    RenderPreview(source.Bounds); // with the tint again
                }

                canvas.BackgroundSurfaceHidden = false;
                canvas.ForegroundLayers = (showLayersAbove && layersAboveLayers != null) ? layersAboveLayers.ToArray() : null;
                canvas.Surface = surface;
            }

            UpdateStatus();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            // the key release goes to whichever window has focus by then
            SetComparing(false);
            SetCompositing(false);
            base.OnDeactivate(e);
        }

        private void mesh_Click(object sender, EventArgs e)
        {
            // the two sizes are alternatives: turning one on turns the other off
            if (sender == meshSmall && meshSmall.Checked)
            {
                meshLarge.Checked = false;
            }
            else if (sender == meshLarge && meshLarge.Checked)
            {
                meshSmall.Checked = false;
            }

            UpdateGrid();
        }

        private void clearMask_Click(object sender, EventArgs e)
        {
            ChangeWholeMesh(mesh.ClearMask);
        }

        private void invertMask_Click(object sender, EventArgs e)
        {
            ChangeWholeMesh(mesh.InvertMask);
        }

        private void resetAll_Click(object sender, EventArgs e)
        {
            ChangeWholeMesh(mesh.ClearOffsets);
        }

        // An edit to the whole mesh as one undoable step.
        private void ChangeWholeMesh(Action change)
        {
            if (strokePending)
            {
                return;
            }

            // The whole mesh is written to disk twice here, which takes a moment on a large image.
            bool recorded = false;
            Cursor previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                // if it can't be made undoable it isn't done, or is put back by AddHistoryItem
                if (historystack.BeforeChange(mesh, mesh.Bounds))
                {
                    change();
                    recorded = historystack.AddHistoryItem(mesh, mesh.Bounds);
                    UpdateHistoryButtons();
                    RenderWholePreview();
                }
            }
            finally
            {
                Cursor.Current = previous;
            }

            if (!recorded)
            {
                ConfirmDialog.Notify(this, this.Text, NotUndoableMessage(), SystemIcons.Warning);
            }
        }

        // What the user can do about it, for the end of a message about running out of memory or
        // disk space. It names the one that ran out, and the drive when it is the disk.
        private static string WhatToDo(bool memory, bool disk)
        {
            // undo data goes to the temporary files folder
            string drive = Path.GetPathRoot(Path.GetTempPath());
            string where = string.IsNullOrEmpty(drive) ? "the disk" : "drive " + drive.TrimEnd('\\');

            string action =
                memory && disk ? "Close other programs to free up memory, and free up space on " + where + "," :
                disk ? "Free up space on " + where :
                "Close other programs to free up memory";

            return "\n\n" + action + " before carrying on. Your earlier work is safe: OK in the Liquify window applies it to the layer.";
        }

        // for a whole-mesh change that history just refused or put back
        private string NotUndoableMessage()
        {
            bool disk = historystack.LastFailure == HistoryFailure.Disk;
            return "That couldn't be done: there isn't enough " + (disk ? "disk space" : "memory") + " to keep it undoable. Nothing was changed."
                + WhatToDo(!disk, disk);
        }

        // Space is the pan key (see CanvasPanel), so don't let it press whichever button has focus.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Space && !brushSize.Focused && !zoom.Focused)
            {
                return true;
            }

            // Enter in the size box sets the size. Left alone, it would press OK.
            if (keyData == Keys.Enter && brushSize.Focused && !brushSize.DroppedDown)
            {
                ResetInvalidBrushSize();
                canvas.Focus();
                return true;
            }

            // the same zoom shortcuts as Paint.NET's main window
            switch (keyData)
            {
                case Keys.Control | Keys.Oemplus:
                case Keys.Control | Keys.Add:
                    canvas.ZoomIn();
                    return true;

                case Keys.Control | Keys.OemMinus:
                case Keys.Control | Keys.Subtract:
                    canvas.ZoomOut();
                    return true;

                case Keys.Control | Keys.D0:
                case Keys.Control | Keys.NumPad0:
                    canvas.ZoomFactor = 1f;
                    return true;

                case Keys.Control | Keys.B:
                    canvas.ZoomToFit();
                    return true;
            }

            // plain letters, unless they are being typed into the size box
            if (!brushSize.Focused)
            {
                switch (keyData)
                {
                    case Keys.P: push.Checked = true; return true;
                    case Keys.L: twistleft.Checked = true; return true;
                    case Keys.R: twistright.Checked = true; return true;
                    case Keys.B: bloat.Checked = true; return true;
                    case Keys.S: pucker.Checked = true; return true;
                    case Keys.E: reconstruct.Checked = true; return true;
                    case Keys.F: freeze.Checked = true; return true;
                    case Keys.T: thaw.Checked = true; return true;

                    case Keys.M:
                        showMask.Checked = !showMask.Checked;
                        showMask_Click(showMask, EventArgs.Empty);
                        return true;

                    case Keys.O:
                        // held down: this repeats, and OnKeyUp ends it
                        SetComparing(true);
                        return true;

                    case Keys.A:
                        SetCompositing(true);
                        return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool renderErrorShown;

        // Runs on the render thread when the brush code throws. The renderer carries on, so the
        // stroke still ends and can be undone.
        void renderer_Error(object sender, ThreadExceptionEventArgs e)
        {
            if (closing)
            {
                return;
            }

            string message = "Something went wrong while the brush was being applied, so part of that stroke may be missing. It can be undone.\n\n" + e.Exception.Message;

            try
            {
                this.BeginInvoke(new Action(() =>
                {
                    if (!closing && !renderErrorShown)
                    {
                        renderErrorShown = true;
                        ConfirmDialog.Notify(this, this.Text, message, SystemIcons.Warning);
                    }
                }));
            }
            catch (InvalidOperationException)
            {
                // the window is already gone
            }
        }

        void renderer_MouseUp(object sender, QueuedToolEventArgs e)
        {
            // Runs on the render thread, which owns the mesh until the stroke has been recorded. So
            // the stroke is written out for undo right here: that can take a second for a large
            // one, and this way the window stays live meanwhile. The UI thread only adds the step.
            if (closing)
            {
                return;
            }

            Rectangle strokeRect = renderer.PopTotalInvalidRect();
            HistoryItem? item = null;
            HistoryFailure failure = HistoryFailure.None;
            Exception notPutBack = null;
            try
            {
                failure = historystack.CaptureChange(mesh, strokeRect, out item);
            }
            catch (Exception ex)
            {
                notPutBack = ex;
            }

            bool recorded = failure == HistoryFailure.None && notPutBack == null;
            if (!recorded && !closing)
            {
                // the stroke has been taken back out of the mesh
                RenderPreview(Rectangle.Intersect(strokeRect, source.Bounds));
                canvas.InvalidateCanvas();
            }

            try
            {
                this.BeginInvoke(new Action(() =>
                {
                    if (closing)
                    {
                        if (item.HasValue)
                        {
                            item.Value.Dispose();
                        }
                        return;
                    }

                    if (item.HasValue)
                    {
                        historystack.Commit(item.Value);
                    }

                    SetStrokePending(false);

                    bool stopped = strokeRanOutOfMemory;
                    strokeRanOutOfMemory = false;

                    if (closing || (!stopped && recorded))
                    {
                        return;
                    }

                    // one message, whatever went wrong
                    string message;
                    if (notPutBack != null)
                    {
                        message = "That stroke couldn't be recorded for undo, and it couldn't be taken back out either. Undo may not work as expected from here on. OK still applies what you see.\n\n" + notPutBack.Message;
                    }
                    else
                    {
                        // a stroke only ever stops for want of memory; recording it can fail on either
                        bool disk = failure == HistoryFailure.Disk;
                        bool memory = stopped || failure == HistoryFailure.Memory;

                        message =
                            recorded ? "There wasn't enough memory to continue that stroke, so it was stopped. What it had done so far can be undone." :
                            stopped && disk ? "There wasn't enough memory to continue that stroke, and not enough disk space to record what it had done for undo, so it has been undone." :
                            stopped ? "There wasn't enough memory to continue that stroke, or to record what it had done for undo, so it has been undone." :
                            disk ? "There wasn't enough disk space to record that stroke for undo, so it has been undone." :
                            "There wasn't enough memory to record that stroke for undo, so it has been undone.";
                        message += WhatToDo(memory, disk);
                    }

                    ConfirmDialog.Notify(this, this.Text, message, SystemIcons.Warning);
                }));
            }
            catch (InvalidOperationException)
            {
                // the window is already gone
                if (item.HasValue)
                {
                    item.Value.Dispose();
                }
            }
        }

        // The controls stop working straight away (their handlers check strokePending), but only
        // look disabled if the stroke lasts.
        private void SetStrokePending(bool pending)
        {
            if (pending)
            {
                ++strokesPending;
                lockTimer.Start();
                return;
            }

            strokesPending = Math.Max(0, strokesPending - 1);
            if (strokePending)
            {
                return; // another stroke is still queued
            }

            lockTimer.Stop();
            ShowControlsLocked(false);

            if (okWhenStrokeEnds)
            {
                okWhenStrokeEnds = false;
                ok.PerformClick();
            }
        }

        private void ShowControlsLocked(bool locked)
        {
            controlsLocked = locked;
            UpdateHistoryButtons();
            ok.Enabled = !locked;
            load.Enabled = !locked;
            save.Enabled = !locked;
            resetAll.Enabled = !locked;
            clearMask.Enabled = !locked;
            invertMask.Enabled = !locked;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // A modal dialog closed by one of its buttons (or Esc, which presses Cancel) reports no
            // reason at all; only the close box reports UserClosing.
            if (e.CloseReason == CloseReason.None || e.CloseReason == CloseReason.UserClosing)
            {
                if (this.DialogResult == DialogResult.OK)
                {
                    if (strokePending)
                    {
                        // the mesh is still being written to: finish the stroke, then close
                        okWhenStrokeEnds = true;
                        e.Cancel = true;
                    }
                }
                else if (historystack != null && historystack.CanStepBack)
                {
                    // Esc and the close box end up here too, and would otherwise throw the work away
                    if (!ConfirmDialog.Show(this, this.Text, "Discard the changes you have made?", "Discard", "Keep editing", SystemIcons.Warning))
                    {
                        e.Cancel = true;
                    }
                }
            }

            base.OnFormClosing(e);
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

        // How the dialog was last closed, whichever way that was, for as long as Paint.NET runs.
        // Not in the token: that is only kept on OK.
        private static DialogSettings lastSettings = new DialogSettings();
        private static Rectangle lastBounds; // empty until the dialog has been closed once
        private static bool lastMaximized;

        private void RememberSettings()
        {
            DialogSettings settings = new DialogSettings();
            settings.pressure = Pressure;
            settings.density = Density;
            settings.size = BrushSize;
            StoreBackground(settings);
            settings.showLayersAbove = showLayersAbove;
            settings.surroundColor = canvas.BackColor.ToArgb();
            settings.meshGrid = meshSmall.Checked ? 1 : meshLarge.Checked ? 2 : 0;
            lastSettings = settings;

            lastMaximized = this.WindowState == FormWindowState.Maximized;
            lastBounds = this.WindowState == FormWindowState.Normal ? this.Bounds : this.RestoreBounds;
        }

        // Where the dialog was last time if that is still on a screen, otherwise over Paint.NET's window.
        private void PlaceWindow()
        {
            bool onScreen = false;
            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle visible = Rectangle.Intersect(screen.WorkingArea, lastBounds);
                onScreen |= visible.Width >= 100 && visible.Height >= 100;
            }

            if (onScreen)
            {
                this.Bounds = lastBounds;
                this.WindowState = lastMaximized ? FormWindowState.Maximized : FormWindowState.Normal;
                return;
            }

            int top = (int)Math.Round(30 * DpiScale);
            this.DesktopLocation = Owner.PointToScreen(new Point(0, top));
            this.Size = new Size(Owner.ClientSize.Width, Owner.ClientSize.Height - top);
            this.WindowState = Owner.WindowState;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            RememberSettings();
            StopRenderer();
            base.OnFormClosed(e);
        }

        private void renderer_Invalidated(object sender, InvalidateEventArgs e)
        {
            if (closing)
            {
                return;
            }

            RenderPreview(e.InvalidRect);
            canvas.InvalidateCanvas(e.InvalidRect);
        }

        // The layer is read in place, not copied: it stays locked while the dialog is open, and
        // source is a view of it.
        private IEffectInputBitmap<ColorBgra32> sourceBitmap;
        private IBitmapLock<ColorBgra32> sourceLock;

        private unsafe void LockSource()
        {
            SizeInt32 docSize = Environment.Document.Size;

            sourceBitmap = Environment.GetSourceBitmapBgra32();
            sourceLock = sourceBitmap.Lock(new RectInt32(0, 0, docSize.Width, docSize.Height));

            // ColorBgra32 and ColorBgra are the same four bytes
            source = new BitmapSurface((ColorBgra*)sourceLock.Buffer, docSize.Width, docSize.Height, sourceLock.BufferStride);
        }

        // Only after the render thread has stopped and the canvas has let go of source.
        private void UnlockSource()
        {
            source = null;

            if (sourceLock != null)
            {
                sourceLock.Dispose();
                sourceLock = null;
            }

            if (sourceBitmap != null)
            {
                sourceBitmap.Dispose();
                sourceBitmap = null;
            }
        }

        private void toolRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton button = sender as RadioButton;
            if (button.Checked)
            {
                if (rendermodes.ContainsKey(button))
                    mode = rendermodes[button];

                UpdateStatus();
            }
        }

        // The line at the bottom of the dialog: what the current tool does and its key, then the
        // things that have no control of their own to hover over. Whatever doesn't fit is cut off
        // with an ellipsis, so the tool comes first.
        private void UpdateStatus()
        {
            const string hints = "Space-drag: pan";

            if (comparing)
            {
                status.Text = "Showing the original image.";
            }
            else if (compositing)
            {
                status.Text = "Showing all layers: the document as it will look with this layer's changes.";
            }
            else
            {
                status.Text = GetToolHint(mode) + "      " + hints;
            }
        }

        private static string GetToolHint(LiquifyMode mode)
        {
            switch (mode)
            {
                case LiquifyMode.Push: return "Push (P): drag to push the image along.";
                case LiquifyMode.TwistLeft: return "Twist left (L): hold or drag to twist the image.";
                case LiquifyMode.TwistRight: return "Twist right (R): hold or drag to twist the image.";
                case LiquifyMode.Bloat: return "Bloat (B): hold or drag to swell the image.";
                case LiquifyMode.Pucker: return "Pucker (S): hold or drag to pinch the image.";
                case LiquifyMode.Reconstruct: return "Reconstruct (E): hold or drag to undo the distortion.";
                case LiquifyMode.Freeze: return "Freeze (F): paint over areas to protect them.";
                case LiquifyMode.Thaw: return "Thaw (T): paint over frozen areas to unprotect them.";
                default: return string.Empty;
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
            ShowBrushPreview();
        }

        private void UpdateBrushInnerRing()
        {
            canvas.BrushInnerFraction = LiquifyRenderer.HalfStrengthRadius(density.Value);
            ShowBrushPreview();
        }

        // The mouse is on the toolbar while the brush is being changed there, so show the brush
        // on the canvas for a moment.
        private void ShowBrushPreview()
        {
            if (brushPreviewEnabled)
            {
                canvas.ShowBrushPreview();
            }
        }

        private void donate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            LaunchUrl("https://forums.paint.net/index.php?showtopic=7291");
        }

        private void ConfigDialog_Load(object sender, EventArgs e)
        {
            int topMargin = (int)Math.Round(6 * DpiScale);
            settingStrip.Location = new Point(settingStrip.Location.X, settingStrip.Location.Y + topMargin);

            int newCanvasTop = settingStrip.Bottom;
            int topDelta = newCanvasTop - canvas.Top;
            canvas.Bounds = new Rectangle(canvas.Left, newCanvasTop, canvas.Width, canvas.Height - topDelta);

            LockSource();
            surface = new Surface(source.Size);
            canvas.Surface = surface;

            canvas.Selection = CreateSelectionRegion();

            mesh = new DisplacementMesh(source.Size);
            mesh.Render(surface, source, source.Bounds);
            historystack = new HistoryStack();

            InitializeRenderer();

            AddLayerBackgrounds();

            if (!HasLayersAbove)
            {
                layersAbove.Enabled = false;
                layersAbove.Text = "Layers above (none)";
            }

            fitZoomPending = true;
            canvas.ZoomToFit();

            PlaceWindow();
        }

        // The canvas's "Background" menu can show the layers under the one being edited. A full-size
        // picture of them is only kept while it is the background in use; the small previews next to
        // the menu entries are made once, from a sample of each layer, and kept.
        private Surface layerBackground;               // the one handed to the canvas, if any
        private CanvasBackground layerBackgroundKind;  // which one that is
        private CanvasBackgroundOption layerBeneathOption;
        private CanvasBackgroundOption allLayersBeneathOption;
        private Image layerBeneathPreview;
        private Image allLayersBeneathPreview;

        // a remembered choice that can't be shown on this layer, kept so that it isn't forgotten
        private CanvasBackground unavailableBackground = CanvasBackground.Color;

        private void AddLayerBackgrounds()
        {
            // layer 0 is the bottom of the stack
            if (Environment.SourceLayerIndex <= 0)
            {
                canvas.BackgroundOptions.Add(CanvasBackgroundOption.Unavailable("Layer beneath (no layers beneath)"));
                canvas.BackgroundOptions.Add(CanvasBackgroundOption.Unavailable("All layers beneath (no layers beneath)"));
                return;
            }

            canvas.BackgroundSurfaceChanged += (s, e) => FreeUnusedLayerBackground();

            layerBeneathOption = new CanvasBackgroundOption(
                "Layer beneath",
                () => UseLayerBackground(CanvasBackground.LayerBeneath),
                () => GetLayerBackgroundPreview(CanvasBackground.LayerBeneath, ref layerBeneathPreview));

            allLayersBeneathOption = new CanvasBackgroundOption(
                "All layers beneath",
                () => UseLayerBackground(CanvasBackground.AllLayersBeneath),
                () => GetLayerBackgroundPreview(CanvasBackground.AllLayersBeneath, ref allLayersBeneathPreview));

            canvas.BackgroundOptions.Add(layerBeneathOption);
            canvas.BackgroundOptions.Add(allLayersBeneathOption);
        }

        // Which layers a background is made of: Layers[first] up to Layers[end], and whether hidden
        // ones are left out.
        private void GetLayerRange(CanvasBackground kind, out int first, out int end, out bool visibleOnly)
        {
            int index = Environment.SourceLayerIndex;

            if (kind == CanvasBackground.LayerBeneath)
            {
                // just that layer, at its own opacity, whether or not it is currently hidden
                first = index - 1;
                end = index;
                visibleOnly = false;
            }
            else
            {
                // as the document looks under this layer: hidden layers left out, blend modes applied
                first = 0;
                end = index;
                visibleOnly = true;
            }
        }

        // Called when a menu entry is picked. The canvas then makes the result its background, which
        // raises BackgroundSurfaceChanged, which frees the one it replaces.
        private Surface UseLayerBackground(CanvasBackground kind)
        {
            if (layerBackground == null || layerBackgroundKind != kind)
            {
                int first, end;
                bool visibleOnly;
                GetLayerRange(kind, out first, out end, out visibleOnly);

                Surface previous = layerBackground;
                CanvasBackground previousKind = layerBackgroundKind;

                if (kind == CanvasBackground.AllLayersBeneath && layersBeneathSurface != null)
                {
                    // "all layers" already made this picture
                    layerBackground = layersBeneathSurface;
                    layersBeneathSurface = null;
                }
                else
                {
                    layerBackground = LayerCompositor.Render(Environment.Document, first, end, visibleOnly);
                }
                layerBackgroundKind = kind;

                if (previous != null)
                {
                    // the canvas is still showing it, so let go of that first
                    canvas.BackgroundSurface = layerBackground;
                    ReleaseLayerBackground(previous, previousKind);
                }
            }

            return layerBackground;
        }

        // The "all layers beneath" picture is also what "all layers" shows, and may be on the canvas
        // for that right now, so once that has been used it is kept instead of freed.
        private void ReleaseLayerBackground(Surface background, CanvasBackground kind)
        {
            if (kind == CanvasBackground.AllLayersBeneath && allLayersShown && layersBeneathSurface == null)
            {
                layersBeneathSurface = background;
            }
            else
            {
                background.Dispose();
            }
        }

        private Image GetLayerBackgroundPreview(CanvasBackground kind, ref Image preview)
        {
            if (preview == null)
            {
                int first, end;
                bool visibleOnly;
                GetLayerRange(kind, out first, out end, out visibleOnly);

                // from a few sampled rows of each layer, so opening the menu doesn't have to wait
                // for full-size pictures
                preview = LayerCompositor.RenderPreview(Environment.Document, first, end, visibleOnly);
            }

            return preview;
        }

        // The user picked something else from the menu (a color, the clipboard image), so the layer
        // picture isn't being shown any more.
        private void FreeUnusedLayerBackground()
        {
            if (layerBackground != null && canvas.BackgroundSurface != layerBackground)
            {
                ReleaseLayerBackground(layerBackground, layerBackgroundKind);
                layerBackground = null;
            }
        }

        private void DisposeLayerBackgrounds()
        {
            canvas.BackgroundSurface = null;
            FreeUnusedLayerBackground();

            FreeLayersAbove();

            if (layersBeneathSurface != null)
            {
                layersBeneathSurface.Dispose();
                layersBeneathSurface = null;
            }

            if (layerBeneathPreview != null)
            {
                layerBeneathPreview.Dispose();
                layerBeneathPreview = null;
            }

            if (allLayersBeneathPreview != null)
            {
                allLayersBeneathPreview.Dispose();
                allLayersBeneathPreview = null;
            }
        }

        // The View menu can lay the layers above the one being edited over the canvas, to judge the
        // result as it will look in the document. The canvas blends them onto the image as it
        // paints, each with its own blend mode (see LayerCompositor.AppendForeground).
        private List<CanvasForegroundLayer> layersAboveLayers;

        // the remembered setting, kept as it was when this layer has nothing above it
        private bool showLayersAbove;

        private bool HasLayersAbove
        {
            get { return Environment.SourceLayerIndex < Environment.Document.Layers.Count - 1; }
        }

        private void SetLayersAbove(bool show)
        {
            showLayersAbove = show;

            if (!HasLayersAbove)
            {
                return;
            }

            layersAbove.Checked = show;

            if (show)
            {
                EnsureLayersAbove();
                canvas.ForegroundLayers = layersAboveLayers.ToArray();
            }
            else
            {
                FreeLayersAbove();
            }
        }

        private void EnsureLayersAbove()
        {
            if (layersAboveLayers != null)
            {
                return;
            }

            Cursor previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                layersAboveLayers = LayerCompositor.RenderForeground(Environment.Document, Environment.SourceLayerIndex + 1, Environment.Document.Layers.Count);
            }
            finally
            {
                Cursor.Current = previous;
            }
        }

        private void FreeLayersAbove()
        {
            canvas.ForegroundLayers = null;

            if (layersAboveLayers != null)
            {
                foreach (CanvasForegroundLayer layer in layersAboveLayers)
                {
                    layer.Surface.Dispose();
                }
                layersAboveLayers = null;
            }
        }

        private void layersAbove_Click(object sender, EventArgs e)
        {
            SetLayersAbove(layersAbove.Checked);
        }

        // The background the dialog was last closed with.
        private void ApplyBackground(CanvasBackground background, int colorArgb)
        {
            CanvasBackgroundOption option =
                background == CanvasBackground.LayerBeneath ? layerBeneathOption :
                background == CanvasBackground.AllLayersBeneath ? allLayersBeneathOption :
                null;

            if (option != null)
            {
                canvas.SelectBackgroundOption(option);
            }
            else if (background != CanvasBackground.Color)
            {
                // there is nothing beneath this layer: show the default, but don't forget the choice
                unavailableBackground = background;
            }
            else
            {
                Color color = Color.FromArgb(colorArgb);
                canvas.CanvasBackColor = color.A == 0 ? Color.Transparent : color;
            }
        }

        private void StoreBackground(DialogSettings token)
        {
            CanvasBackgroundOption active = canvas.ActiveBackgroundOption;

            if (active != null && active == layerBeneathOption)
            {
                token.background = CanvasBackground.LayerBeneath;
            }
            else if (active != null && active == allLayersBeneathOption)
            {
                token.background = CanvasBackground.AllLayersBeneath;
            }
            else if (unavailableBackground != CanvasBackground.Color && canvas.CanvasBackColor.A == 0)
            {
                // it couldn't be shown this time and nothing else was picked in its place
                token.background = unavailableBackground;
            }
            else
            {
                token.background = CanvasBackground.Color;
                token.backgroundColor = canvas.CanvasBackColor.ToArgb();
            }
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

        private const int WM_MOUSEHWHEEL = 0x020E;

        // Sideways wheel and two-finger touchpad scrolling. WinForms has no event for it; it arrives
        // here when it was sent to a control other than the canvas, which handles its own.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEHWHEEL)
            {
                canvas.PerformHorizontalMouseWheel(CanvasPanel.WheelDelta(m));

                // not 0: see CanvasPanel.HandleWheelMessage
                m.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref m);
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
                    // Ctrl+Shift+Z arrives as the same character
                    if ((ModifierKeys & Keys.Shift) != 0)
                    {
                        DoRedo();
                    }
                    else
                    {
                        DoUndo();
                    }
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
                StepHistory(historystack.StepBack, "undone");
            }
        }

        private void DoRedo()
        {
            if (historystack.CanStepForward && !strokePending)
            {
                StepHistory(historystack.StepForward, "redone");
            }
        }

        // A step is read back from disk into new memory, and either can fail. The mesh is then
        // as it was.
        private void StepHistory(Func<DisplacementMesh, Rectangle> step, string done)
        {
            // reading a large step back and redrawing it takes a moment
            Cursor previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                Rectangle rect;
                try
                {
                    rect = step(mesh);
                }
                catch (Exception ex)
                {
                    Cursor.Current = previous;

                    string reason = ex is OutOfMemoryException
                        ? "there isn't enough memory. Close other programs to free some up, then try again."
                        : "its saved data couldn't be read back from the temporary files folder.\n\n" + ex.Message;

                    ConfirmDialog.Notify(this, this.Text,
                        "That step couldn't be " + done + ": " + reason + "\n\nNothing was changed.",
                        SystemIcons.Warning);
                    return;
                }

                UpdateHistoryButtons();
                RenderPreview(rect);
                canvas.InvalidateCanvas(rect);
            }
            finally
            {
                Cursor.Current = previous;
            }
        }

        private void UpdateHistoryButtons()
        {
            redo.Enabled = historystack.CanStepForward && !controlsLocked;
            undo.Enabled = historystack.CanStepBack && !controlsLocked;
        }

        public int BrushSize
        {
            get
            {
                // the text box only flags bad input, so it is caught here: the renderer can't handle sizes below 2
                float width;
                return TryParseBrushSize(out width) ? (int)width : lastValidBrushSize;
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

        // The eraser end of a pen takes away what the current tool puts down: it thaws when the
        // tool is Freeze (and freezes when it is Thaw), and otherwise undoes the distortion.
        private LiquifyMode ModeFor(CanvasMouseEventArgs e)
        {
            if (!e.Eraser)
            {
                return mode;
            }

            switch (mode)
            {
                case LiquifyMode.Freeze: return LiquifyMode.Thaw;
                case LiquifyMode.Thaw: return LiquifyMode.Freeze;
                default: return LiquifyMode.Reconstruct;
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
                    Pressure * e.Pressure, // pen pressure, 1 for a mouse
                    Density,
                    ModeFor(e)));
        }

        private void canvas_CanvasMouseDown(object sender, CanvasMouseEventArgs e)
        {
            // Clicking the canvas doesn't take focus away from the size box by itself. Move it, so the
            // letter shortcuts work again, and check the size here in case the focus change didn't.
            if (brushSize.Focused)
            {
                canvas.Focus();
            }
            ResetInvalidBrushSize();

            SetStrokePending(true);
            renderer.AddEvent(
                new LiquifyEventArgs(
                    QueuedToolEventType.MouseDown,
                    e.Button,
                    (int)e.X,
                    (int)e.Y,
                    BrushSize,
                    Pressure * e.Pressure, // pen pressure, 1 for a mouse
                    Density,
                    ModeFor(e)));
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
                    Pressure * e.Pressure, // pen pressure, 1 for a mouse
                    Density,
                    ModeFor(e)));
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
                    Pressure * e.Pressure, // pen pressure, 1 for a mouse
                    Density,
                    ModeFor(e)));

        }


        private void zoom_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (updatingZoomBox || zoom.SelectedIndex < 0)
            {
                return;
            }

            if (zoom.SelectedIndex >= CanvasPanel.ZoomFactors.Length)
            {
                canvas.ZoomToFit();
            }
            else
            {
                canvas.ZoomFactor = CanvasPanel.ZoomFactors[zoom.SelectedIndex];
            }
        }


        // The token holds nothing the dialog shows: every session starts from an empty mesh. This
        // is where the dialog is set up, from the settings it was last closed with.
        protected override void OnUpdateDialogFromToken(ConfigToken token)
        {
            DialogSettings settings = lastSettings;

            Pressure = settings.pressure;
            Density = settings.density;
            BrushSize = settings.size;
            ApplyBackground(settings.background, settings.backgroundColor);
            SetLayersAbove(settings.showLayersAbove);

            if (settings.surroundColor != 0)
            {
                canvas.BackColor = Color.FromArgb(settings.surroundColor);
            }

            meshSmall.Checked = settings.meshGrid == 1;
            meshLarge.Checked = settings.meshGrid == 2;
            UpdateGrid();
        }

        protected override void OnUpdateTokenFromDialog(ConfigToken token)
        {
            token.mesh = mesh;
        }

        private void ok_Click(object sender, EventArgs e)
        {
            UpdateTokenFromDialog();
        }

        const string dialogFilter = "Liquify mesh, Photoshop compatible (*.msh)|*.msh";
        private void load_Click(object sender, EventArgs e)
        {
            if (strokePending)
            {
                return;
            }

            using OpenFileDialog ofd = new OpenFileDialog();

            ofd.Filter = dialogFilter;
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                // reading the file and saving the mesh for undo take a moment on a large image;
                // the cursor goes back by itself once that is over
                Cursor.Current = Cursors.WaitCursor;

                try
                {
                    if (!historystack.BeforeChange(mesh, mesh.Bounds))
                    {
                        ConfirmDialog.Notify(this, this.Text, NotUndoableMessage(), SystemIcons.Warning);
                        return;
                    }

                    using (FileStream fs = new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read))
                    {
                        mesh.Load(fs);
                    }

                    // a loaded mesh is an edit like any other, so it can be undone
                    bool recorded = historystack.AddHistoryItem(mesh, mesh.Bounds);
                    UpdateHistoryButtons();
                    RenderPreview(surface.Bounds);
                    canvas.InvalidateCanvas();

                    if (!recorded)
                    {
                        ConfirmDialog.Notify(this, this.Text, NotUndoableMessage(), SystemIcons.Warning);
                    }
                }
                catch (Exception exception)
                {
                    // put back whatever the load had already overwritten
                    string notRestored = string.Empty;
                    try
                    {
                        historystack.RevertChange(mesh);
                    }
                    catch
                    {
                        notRestored = "\n\nThe mesh may have been partly replaced, and that can't be undone.";
                    }
                    RenderWholePreview();

                    MessageBox.Show(this,
                        "Error loading mesh from file:\n\n" + exception.ToString() + notRestored,
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

            using SaveFileDialog sfd = new SaveFileDialog();
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
