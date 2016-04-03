using PaintDotNet;
using pyrochild.effects.liquify;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace pyrochild.effects.common
{
    public partial class CanvasPanel : UserControl
    {
        private Surface surface;
        private float scale;
        private MouseButtons buttons;
        private int brushRadius;
        private int brushSize;
        private PointF canvasmouselocation; //the mouse location relative to the PICTUREBOX
        private bool panelhasmouse;
        private bool canvashasmouse;
        private PdnRegion selection;
        private PdnRegion unSelection;
        private PdnRegion selectionOutline;
        private Brush tintbrush;
        private Brush outlinebrush;

        public CanvasPanel()
        {
            InitializeComponent();
            this.ZoomFactor = 1.0f;
            this.CanvasBackColor = Color.Transparent;

            tintbrush = new SolidBrush(Color.FromArgb(63, 0, 0, 0));
            outlinebrush = SystemBrushes.Highlight;
        }

        void canvas_Paint(object sender, PaintEventArgs e)
        {
            DrawSelection(e.Graphics);

            if (panelhasmouse || canvashasmouse)
            {
                int scaledbrushradius = (int)(brushRadius * scale);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawEllipse(
                    Pens.Black,
                    (int)canvasmouselocation.X - scaledbrushradius - 1,
                    (int)canvasmouselocation.Y - scaledbrushradius - 1,
                    2 * scaledbrushradius + 2,
                    2 * scaledbrushradius + 2);
            }
        }

        void CanvasPanel_Paint(object sender, PaintEventArgs e)
        {
            if (panelhasmouse || canvashasmouse)
            {
                int scaledbrushradius = (int)(brushRadius * scale);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawEllipse(
                    Pens.Black,
                    (int)canvasmouselocation.X - scaledbrushradius - 1 + canvas.Location.X,
                    (int)canvasmouselocation.Y - scaledbrushradius - 1 + canvas.Location.Y,
                    2 * scaledbrushradius + 2,
                    2 * scaledbrushradius + 2);
            }
        }

        private void DrawSelection(Graphics gdiG)
        {
            if (selection == null)
            {
                return;
            }
            gdiG.ScaleTransform(scale, scale);

            DrawSelectionTinting(gdiG);

            gdiG.ScaleTransform(1 / scale, 1 / scale);

            DrawSelectionOutline(gdiG);
        }

        private void DrawSelectionOutline(Graphics g)
        {
            if (selectionOutline == null)
            {
                return;
            }
            g.FillRegion(outlinebrush, selectionOutline.GetRegionReadOnly());
        }

        private void DrawSelectionTinting(Graphics g)
        {
            if (unSelection == null)
            {
                return;
            }

            CompositingMode oldCM = g.CompositingMode;
            g.CompositingMode = CompositingMode.SourceOver;

            g.FillRegion(tintbrush, unSelection.GetRegionReadOnly());

            g.CompositingMode = oldCM;
        }

        public static readonly float[] ZoomFactors = 
            { 
                .01f, .02f, .03f, .04f, .05f, .06f, .08f, .12f, .16f, .25f, .33f, .5f, .66f, 1,
                1.5f, 2, 3, 4, 5, 6, 7, 8, 12, 16
            };

        public int MouseHoldInterval
        {
            get { return holdTimer.Interval; }
            set { holdTimer.Interval = value; }
        }

        public int BrushSize
        {
            get
            {
                return brushSize;
            }
            set
            {
                InvalidateBrush();
                brushSize = value;
                brushRadius = value / 2;
                InvalidateBrush();
            }
        }

        public PdnRegion Selection
        {
            get
            {
                return selection;
            }
            set
            {
                selection = value;
                InvalidateSelection();
            }
        }

        private void InvalidateSelection()
        {
            if (selection != null)
            {
                unSelection = new PdnRegion(new Rectangle(0, 0, surface.Width, surface.Height));
                unSelection.Exclude(selection);
                selectionOutline = selection.GetOutline(surface.Bounds, scale);
            }
            canvas.Invalidate();
        }

        public Surface Surface
        {
            get
            {
                return surface;
            }
            set
            {
                surface = value;
                if (surface != null)
                {
                    canvas.Image = surface.CreateAliasedBitmap();
                    UpdateSize();
                }
            }
        }

        public Color CanvasBackColor
        {
            get
            {
                return canvas.BackColor;
            }
            set
            {
                canvas.BackColor = value;
            }
        }

        public void InvalidateCanvas()
        {
            canvas.Invalidate();
        }

        public void InvalidateCanvas(Rectangle invalidRect)
        {
            canvas.Invalidate(invalidRect.Factor(scale));
        }

        private void UpdateSize()
        {
            if (surface != null)
            {
                canvas.Size = surface.Size.Factor(scale);
            }
            this.SetAutoScrollMargin(10, 10);
            CanvasPanel_Resize(this, EventArgs.Empty);
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
        }

        public float ZoomFactor
        {
            get
            {
                return scale;
            }
            set
            {
                if (scale != value)
                {
                    scale = value;
                    UpdateSize();
                    InvalidateSelection();
                    OnZoomFactorChanged();
                    PerformLayout();
                    Invalidate();
                }
            }
        }

        private void holdTimer_Tick(object sender, EventArgs e)
        {
            OnCanvasMouseHold(buttons, canvasmouselocation.X, canvasmouselocation.Y);
        }

        private void canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                ShowContextMenu(canvas, e.Location, false);

            OnCanvasMouseDown(e.Button, e.X, e.Y);
        }

        private void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            OnCanvasMouseMove(e.Button, e.X, e.Y);
        }

        private void canvas_MouseUp(object sender, MouseEventArgs e)
        {
            OnCanvasMouseUp(e.Button, e.X, e.Y);
        }

        private void canvas_MouseLeave(object sender, System.EventArgs e)
        {
            canvashasmouse = false;
            InvalidateBrush();
        }

        private void canvas_MouseEnter(object sender, System.EventArgs e)
        {
            canvashasmouse = true;
        }

        private void CanvasPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                ShowContextMenu(this, e.Location, true);

            OnCanvasMouseDown(e.Button, e.X - canvas.Location.X, e.Y - canvas.Location.Y);
        }

        private void ShowContextMenu(Control sender, Point location, bool colorsOnly)
        {
            contextMenu.Items.Clear();
            using (Surface sfc = new Surface(16, 16))
            {
                contextMenu.Items.Add(new ToolStripLabel("Background"));
                contextMenu.Items.Add(new ToolStripSeparator());

                sfc.ClearWithCheckerboardPattern();
                if (!colorsOnly)
                    contextMenu.Items.Add("Transparent", new Bitmap(sfc.CreateAliasedBitmap()), (s, e) =>
                    {
                        sender.BackgroundImage = null;
                        sender.BackColor = Color.Transparent;
                    });

                sfc.Clear(ColorBgra.Black);
                contextMenu.Items.Add("Black", new Bitmap(sfc.CreateAliasedBitmap()), (s, e) =>
                {
                    sender.BackgroundImage = null;
                    sender.BackColor = Color.Black;
                });

                sfc.Clear(ColorBgra.White);
                contextMenu.Items.Add("White", new Bitmap(sfc.CreateAliasedBitmap()), (s, e) =>
                {
                    sender.BackgroundImage = null;
                    sender.BackColor = Color.White;
                });

                sfc.Clear(ColorBgra.FromBgr(127, 127, 127));
                contextMenu.Items.Add("Gray", new Bitmap(sfc.CreateAliasedBitmap()), (s, e) =>
                {
                    sender.BackgroundImage = null;
                    sender.BackColor = Color.Gray;
                });

                contextMenu.Items.Add("Other color...", new Bitmap(typeof(Liquify),"images.colorwheel.png"), (s, e) =>
                {
                    ColorBgra c;
                    if (DialogResult.OK == ShowColorPicker(sender, location, !colorsOnly, out c))
                    {
                        sender.BackColor = c.ToColor();
                        sender.BackgroundImage = null;
                    }
                });

                if (!colorsOnly)
                {
                    contextMenu.Items.Add("From clipboard", null, (s, e) =>
                    {
                        try
                        {
                            sender.BackgroundImage = Clipboard.GetImage();
                            sender.BackColor = Color.Transparent;
                        }
                        catch { }
                    });
                    if (Clipboard.ContainsImage())
                    {
                        using (Surface fromcb = Surface.CopyFromBitmap((Bitmap)Clipboard.GetImage()))
                        {
                            sfc.FitSurface(ResamplingAlgorithm.SuperSampling, fromcb);
                            contextMenu.Items[7].Image = new Bitmap(sfc.CreateAliasedBitmap());
                        }
                    }
                    else
                    {
                        contextMenu.Items[7].Enabled = false;
                    }
                }
            }
            contextMenu.Show(sender, location);
        }

        private DialogResult ShowColorPicker(Control owner, Point location, bool alpha, out ColorBgra color)
        {
            using (ColorDialog cd = new ColorDialog(alpha))
            {
                cd.Color = ColorBgra.FromColor(owner.BackColor);

                DialogResult result = cd.ShowDialog(owner);
                color = cd.Color;
                return result;
            }
        }

        private void CanvasPanel_MouseMove(object sender, MouseEventArgs e)
        {
            OnCanvasMouseMove(e.Button, e.X - canvas.Location.X, e.Y - canvas.Location.Y);
        }

        private void CanvasPanel_MouseUp(object sender, MouseEventArgs e)
        {
            OnCanvasMouseUp(e.Button, e.X - canvas.Location.X, e.Y - canvas.Location.Y);
        }

        private void CanvasPanel_MouseLeave(object sender, System.EventArgs e)
        {
            panelhasmouse = false;
            InvalidateBrush();
        }

        private void InvalidateBrush()
        {
            int scaledbrushradius = (int)(brushRadius * scale);
            this.Invalidate(new Rectangle(
                (int)canvasmouselocation.X - scaledbrushradius + canvas.Location.X - 2,
                (int)canvasmouselocation.Y - scaledbrushradius + canvas.Location.Y - 2,
                2 * scaledbrushradius + 5,
                2 * scaledbrushradius + 5), true);
        }

        private void CanvasPanel_MouseEnter(object sender, System.EventArgs e)
        {
            panelhasmouse = true;
        }

        private void CanvasPanel_Resize(object sender, System.EventArgs e)
        {
            PerformLayout();
            // If the client area is bigger than the area used to display the image, center it
            int newX = 10 + AutoScrollPosition.X;
            int newY = 10 + AutoScrollPosition.Y;

            if (this.ClientRectangle.Width > canvas.Width + 20)
            {
                newX = AutoScrollPosition.X + ((this.ClientRectangle.Width - canvas.Width) / 2);
            }

            if (this.ClientRectangle.Height > canvas.Height + 20)
            {
                newY = AutoScrollPosition.Y + ((this.ClientRectangle.Height - canvas.Height) / 2);
            }

            canvas.Location = new Point(newX, newY);
        }

        public event EventHandler<CanvasMouseEventArgs> CanvasMouseDown;
        private void OnCanvasMouseDown(MouseButtons button, float x, float y)
        {
            holdTimer.Enabled = true;
            buttons = button;
            if (CanvasMouseDown != null)
                CanvasMouseDown(this, new CanvasMouseEventArgs(button, x / scale, y / scale));
        }

        public event EventHandler<CanvasMouseEventArgs> CanvasMouseMove;
        private void OnCanvasMouseMove(MouseButtons button, float x, float y)
        {
            InvalidateBrush();
            canvasmouselocation = new PointF(x, y);
            InvalidateBrush();
            if (CanvasMouseMove != null)
                CanvasMouseMove(this, new CanvasMouseEventArgs(button, x / scale, y / scale));
        }

        public event EventHandler<CanvasMouseEventArgs> CanvasMouseUp;
        private void OnCanvasMouseUp(MouseButtons button, float x, float y)
        {
            holdTimer.Enabled = false;
            buttons = MouseButtons.None;
            if (CanvasMouseUp != null)
                CanvasMouseUp(this, new CanvasMouseEventArgs(button, x / scale, y / scale));
        }

        public event EventHandler<CanvasMouseEventArgs> CanvasMouseHold;
        private void OnCanvasMouseHold(MouseButtons button, float x, float y)
        {
            if (CanvasMouseHold != null)
                CanvasMouseHold(this, new CanvasMouseEventArgs(button, x / scale, y / scale));
        }

        public event EventHandler ZoomFactorChanged;
        private void OnZoomFactorChanged()
        {
            if (ZoomFactorChanged != null)
                ZoomFactorChanged(this, EventArgs.Empty);
        }

        public void ZoomOut()
        {
            if (scale > ZoomFactors[0])
                for (int i = 1; i < ZoomFactors.Length; ++i)
                    if (scale == ZoomFactors[i])
                    {
                        ZoomFactor = ZoomFactors[i - 1];
                        break;
                    }
        }

        public void ZoomIn()
        {
            if (scale < ZoomFactors[ZoomFactors.Length - 1])
                for (int i = 0; i < ZoomFactors.Length - 1; ++i)
                    if (scale == ZoomFactors[i])
                    {
                        ZoomFactor = ZoomFactors[i + 1];
                        break;
                    }
        }

        public void PerformMouseWheel(MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) != Keys.None)
            {
                PointF documentmouselocation = new PointF(canvasmouselocation.X / scale, canvasmouselocation.Y / scale);

                if (e.Delta > 0)
                {
                    ZoomIn();
                }
                else
                {
                    ZoomOut();
                }

                if (canvashasmouse) //try to keep the mouse over the same virtual location on the document
                    SetScrollLocation(documentmouselocation);
            }
            else if ((ModifierKeys & Keys.Shift) != Keys.None)
            {
                if (e.Delta > 0)
                {
                    HorizontalScroll.Value -= Math.Min(HorizontalScroll.Value - HorizontalScroll.Minimum, e.Delta);
                }
                else
                {
                    HorizontalScroll.Value -= Math.Max(HorizontalScroll.Value - HorizontalScroll.Maximum, e.Delta);
                }
                PerformLayout();
            }
            else
            {
                if (e.Delta > 0)
                {
                    VerticalScroll.Value -= Math.Min(VerticalScroll.Value - VerticalScroll.Minimum, e.Delta);
                }
                else
                {
                    VerticalScroll.Value -= Math.Max(VerticalScroll.Value - VerticalScroll.Maximum, e.Delta);
                }
                PerformLayout();
            }
        }

        private void SetScrollLocation(PointF documentmouselocation)
        {
            PointF desiredcanvasmouselocation = new PointF(documentmouselocation.X * scale, documentmouselocation.Y * scale);
            int dx = (int)(canvasmouselocation.X - desiredcanvasmouselocation.X);
            int dy = (int)(canvasmouselocation.Y - desiredcanvasmouselocation.Y);

            if (dx > 0)
            {
                HorizontalScroll.Value -= Math.Min(HorizontalScroll.Value - HorizontalScroll.Minimum, dx);
            }
            else
            {
                HorizontalScroll.Value -= Math.Max(HorizontalScroll.Value - HorizontalScroll.Maximum, dx);
            }
            if (dy > 0)
            {
                VerticalScroll.Value -= Math.Min(VerticalScroll.Value - VerticalScroll.Minimum, dy);
            }
            else
            {
                VerticalScroll.Value -= Math.Max(VerticalScroll.Value - VerticalScroll.Maximum, dy);
            }
            PerformLayout();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            //do nothing
            //we don't want the panel scrolling vertically when trying to change zoom or scroll horizontally

            //the owner is responsible for calling PerformMouseWheel()
        }
    }

    class PictureBoxEx : Control
    {
        Brush checkerbrush;

        public PictureBoxEx()
        {
            base.SetStyle(ControlStyles.Selectable | ControlStyles.Opaque, false);
            base.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor, true);
            this.TabStop = false;

            using (Surface s = new Surface(16, 16))
            {
                s.ClearWithCheckerboardPattern();
                checkerbrush = new TextureBrush(s.CreateAliasedBitmap(), WrapMode.Tile);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (checkerbrush != null)
                checkerbrush.Dispose();

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            pe.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            pe.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (BackColor.A != 255)
            {
                pe.Graphics.FillRectangle(checkerbrush, pe.ClipRectangle);
            }
            if (BackColor != Color.Transparent)
            {
                using (Brush b = new SolidBrush(BackColor))
                {
                    pe.Graphics.FillRectangle(b, pe.ClipRectangle);
                }
            }
            if (BackgroundImage != null)
            {
                pe.Graphics.DrawImage(BackgroundImage, ClientRectangle);
            }
            if (Image != null)
            {
                pe.Graphics.DrawImage(Image, ClientRectangle);
            }

            base.OnPaint(pe);
        }

        protected override void OnPaintBackground(PaintEventArgs pe)
        {
            //base.OnPaintBackground(pe); Nope!
        }

        public Bitmap Image { get; set; }
    }
}