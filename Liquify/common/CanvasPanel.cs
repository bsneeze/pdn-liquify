using PaintDotNet;
using pyrochild.effects.liquify;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace pyrochild.effects.common
{
    /// <summary>
    /// A scrollable, zoomable view of a Surface with a brush cursor, which raises mouse events in the
    /// surface's coordinates.
    /// The image ("the canvas") is drawn straight onto this control, and scrolling is done here too:
    /// a scroll step just changes ScrollPosition and repaints. ScrollableControl's AutoScroll isn't
    /// used because it scrolls by copying the window's pixels, which at large window sizes was measured
    /// to take about twice as long as repainting the whole view.
    /// </summary>
    public partial class CanvasPanel : UserControl
    {
        private const int canvasMargin = 10;

        private Surface surface;
        private Bitmap image; // shares the surface's memory
        private Size canvasSize; // the image at the current zoom
        private Color canvasBackColor = Color.Transparent;
        private Image canvasBackgroundImage;
        private float scale;
        private MouseButtons buttons;
        private int brushRadius;
        private int brushSize;
        private PointF canvasmouselocation; //the mouse location relative to the canvas
        private bool panelhasmouse;
        private bool canvashasmouse;
        private PdnRegion selection;
        private PdnRegion unSelection;
        private PdnRegion selectionOutline;
        private Brush tintbrush;
        private Brush outlinebrush;
        private Brush outerCheckerBrush;
        private Brush canvasCheckerBrush;
        private uint[] scaled; // scratch pixels for DrawComposited
        private int[] sourceColumns;

        public CanvasPanel()
        {
            InitializeComponent();

            // Painting is buffered by RegionPainter. Not DoubleBuffered: that allocates a buffer the size
            // of the whole panel on every paint.
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            hScroll.Visible = false;
            vScroll.Visible = false;
            hScroll.Cursor = Cursors.Default;
            vScroll.Cursor = Cursors.Default;
            hScroll.ValueChanged += new EventHandler(scrollBar_ValueChanged);
            vScroll.ValueChanged += new EventHandler(scrollBar_ValueChanged);
            this.Controls.Add(hScroll);
            this.Controls.Add(vScroll);

            this.ZoomFactor = 1.0f;

            tintbrush = new SolidBrush(Color.FromArgb(63, 0, 0, 0));
            outlinebrush = SystemBrushes.Highlight;
        }

        // Not Surface.ClearWithCheckerboardPattern(): on some machines it renders as a flat fill.
        internal static Bitmap CreateCheckerboardTile(float dpiScale)
        {
            int cell = Math.Max(4, (int)Math.Round(8 * dpiScale));
            Bitmap bmp = new Bitmap(cell * 2, cell * 2);
            using (Graphics g = Graphics.FromImage(bmp))
            using (Brush light = new SolidBrush(Color.White))
            using (Brush dark = new SolidBrush(Color.FromArgb(191, 191, 191)))
            {
                g.FillRectangle(light, 0, 0, cell, cell);
                g.FillRectangle(dark, cell, 0, cell, cell);
                g.FillRectangle(dark, 0, cell, cell, cell);
                g.FillRectangle(light, cell, cell, cell, cell);
            }
            return bmp;
        }

        private readonly HScrollBar hScroll = new HScrollBar();
        private readonly VScrollBar vScroll = new VScrollBar();
        private Point scrollOffset; // how far the view is scrolled; never negative
        private Size viewport;      // the client area not covered by the scrollbars
        private bool syncingScrollbars;

        // Designers and owners may still set this; it has to stay off for the reason in the class summary.
        public override bool AutoScroll
        {
            get { return false; }
            set { }
        }

        /// <summary>
        /// How far the view is scrolled from the top left, in pixels. Setting it keeps it in range.
        /// </summary>
        public Point ScrollPosition
        {
            get
            {
                return scrollOffset;
            }
            set
            {
                Point old = scrollOffset;
                scrollOffset = value;
                UpdateScrollbars();

                if (scrollOffset != old)
                {
                    this.Invalidate(new Rectangle(Point.Empty, viewport));
                }
            }
        }

        // Works out which scrollbars are needed, lays them out, and brings them and scrollOffset
        // in line with each other.
        private void UpdateScrollbars()
        {
            Size client = ClientSize;
            int barWidth = SystemInformation.GetVerticalScrollBarWidthForDpi(this.DeviceDpi);
            int barHeight = SystemInformation.GetHorizontalScrollBarHeightForDpi(this.DeviceDpi);
            int extentWidth = canvasSize.Width + 2 * canvasMargin;
            int extentHeight = canvasSize.Height + 2 * canvasMargin;

            // one scrollbar takes space away from the other direction, which can make that one needed too
            bool needH = extentWidth > client.Width;
            bool needV = extentHeight > client.Height - (needH ? barHeight : 0);
            if (needV && !needH)
            {
                needH = extentWidth > client.Width - barWidth;
            }

            viewport = new Size(
                Math.Max(0, client.Width - (needV ? barWidth : 0)),
                Math.Max(0, client.Height - (needH ? barHeight : 0)));

            scrollOffset = new Point(
                Math.Max(0, Math.Min(scrollOffset.X, extentWidth - viewport.Width)),
                Math.Max(0, Math.Min(scrollOffset.Y, extentHeight - viewport.Height)));

            syncingScrollbars = true;
            try
            {
                SyncScrollBar(hScroll, needH, new Rectangle(0, viewport.Height, viewport.Width, barHeight), extentWidth, viewport.Width, scrollOffset.X);
                SyncScrollBar(vScroll, needV, new Rectangle(viewport.Width, 0, barWidth, viewport.Height), extentHeight, viewport.Height, scrollOffset.Y);
            }
            finally
            {
                syncingScrollbars = false;
            }
        }

        // Only touches what has changed: every property set makes the scrollbar redraw.
        private static void SyncScrollBar(ScrollBar bar, bool visible, Rectangle bounds, int extent, int page, int value)
        {
            if (visible)
            {
                int largeChange = Math.Max(1, page);
                int smallChange = Math.Max(1, page / 20);

                if (bar.Bounds != bounds) bar.Bounds = bounds;
                if (bar.Maximum != extent - 1) bar.Maximum = extent - 1;
                if (bar.LargeChange != largeChange) bar.LargeChange = largeChange;
                if (bar.SmallChange != smallChange) bar.SmallChange = smallChange;
                if (bar.Value != value) bar.Value = value;
            }

            if (bar.Visible != visible) bar.Visible = visible;
        }

        private void scrollBar_ValueChanged(object sender, EventArgs e)
        {
            if (syncingScrollbars)
            {
                return;
            }

            scrollOffset = new Point(hScroll.Visible ? hScroll.Value : 0, vScroll.Visible ? vScroll.Value : 0);
            this.Invalidate(new Rectangle(Point.Empty, viewport));
            AfterScroll();
        }

        // Where the canvas is within this control: centered if it fits, otherwise wherever it is scrolled to.
        private Point CanvasLocation
        {
            get
            {
                int x = canvasMargin - scrollOffset.X;
                int y = canvasMargin - scrollOffset.Y;

                if (viewport.Width > canvasSize.Width + 2 * canvasMargin)
                {
                    x = (viewport.Width - canvasSize.Width) / 2;
                }

                if (viewport.Height > canvasSize.Height + 2 * canvasMargin)
                {
                    y = (viewport.Height - canvasSize.Height) / 2;
                }

                return new Point(x, y);
            }
        }

        private Rectangle CanvasBounds
        {
            get { return new Rectangle(CanvasLocation, canvasSize); }
        }

        private readonly RegionPainter painter = new RegionPainter();

        protected override void WndProc(ref Message m)
        {
            painter.BeforeWndProc(this, ref m);
            base.WndProc(ref m);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // the background is painted in OnPaint, into the same buffer as everything else
        }

        private bool painting;

        protected override void OnPaint(PaintEventArgs e)
        {
            // DrawComposited runs its rows in parallel, and while this thread waits for them it can be
            // handed another paint message (another thread invalidating the canvas is enough). The two
            // would share the scratch buffer, so put the inner one off until this one is done.
            if (painting)
            {
                Rectangle again = e.ClipRectangle;
                BeginInvoke(new Action(() => this.Invalidate(again)));
                return;
            }

            painting = true;
            try
            {
                PaintNow(e);
            }
            finally
            {
                painting = false;
            }
        }

        private void PaintNow(PaintEventArgs e)
        {
            Rectangle canvasBounds = CanvasBounds;
            Rectangle view = new Rectangle(Point.Empty, viewport);

            painter.Paint(e, ClientRectangle, (g, wholeClip) =>
            {
                // the only part of the client area outside the viewport that isn't under a scrollbar
                // is the square where the two scrollbars meet
                Rectangle clip = Rectangle.Intersect(wholeClip, view);
                if (clip != wholeClip)
                {
                    g.FillRectangle(SystemBrushes.Control, wholeClip);
                }

                if (clip.Width <= 0 || clip.Height <= 0)
                {
                    return;
                }

                g.SetClip(clip);

                if (!canvasBounds.Contains(clip))
                {
                    PaintBackground(g, clip);
                }

                Rectangle canvasClip = Rectangle.Intersect(clip, canvasBounds);
                if (canvasClip.Width > 0 && canvasClip.Height > 0)
                {
                    // the canvas is drawn in its own coordinates
                    GraphicsState state = g.Save();
                    g.TranslateTransform(canvasBounds.X, canvasBounds.Y);
                    canvasClip.Offset(-canvasBounds.X, -canvasBounds.Y);
                    g.SetClip(canvasClip, CombineMode.Intersect);

                    PaintCanvas(g, canvasClip);
                    DrawSelection(g);

                    g.Restore(state);
                }

                if (panelhasmouse)
                {
                    int scaledbrushradius = (int)(brushRadius * scale);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawEllipse(
                        Pens.Black,
                        (int)canvasmouselocation.X - scaledbrushradius - 1 + canvasBounds.X,
                        (int)canvasmouselocation.Y - scaledbrushradius - 1 + canvasBounds.Y,
                        2 * scaledbrushradius + 2,
                        2 * scaledbrushradius + 2);
                }

                using (PaintEventArgs bufferedArgs = new PaintEventArgs(g, clip))
                {
                    base.OnPaint(bufferedArgs);
                }
            });
        }

        // The area around the canvas. The "Background" menu can give it a see-through color, so it
        // needs a checkerboard too.
        private void PaintBackground(Graphics g, Rectangle clip)
        {
            if (BackColor.A != 255)
            {
                if (outerCheckerBrush == null)
                {
                    outerCheckerBrush = new TextureBrush(CreateCheckerboardTile(this.DeviceDpi / 96f), WrapMode.Tile);
                }

                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.FillRectangle(outerCheckerBrush, clip);
            }

            if (BackColor != Color.Transparent)
            {
                using (Brush b = new SolidBrush(BackColor))
                {
                    g.FillRectangle(b, clip);
                }
            }
        }

        // Draws the canvas: its background and the image. g and clip are in canvas coordinates.
        private void PaintCanvas(Graphics g, Rectangle clip)
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (surface != null && canvasBackgroundImage == null)
            {
                DrawComposited(g, clip);
                return;
            }

            Rectangle whole = new Rectangle(Point.Empty, canvasSize);

            if (canvasBackColor.A != 255)
            {
                if (canvasCheckerBrush == null)
                {
                    canvasCheckerBrush = new TextureBrush(CreateCheckerboardTile(this.DeviceDpi / 96f), WrapMode.Tile);
                }

                g.FillRectangle(canvasCheckerBrush, clip);
            }
            if (canvasBackColor != Color.Transparent)
            {
                using (Brush b = new SolidBrush(canvasBackColor))
                {
                    g.FillRectangle(b, clip);
                }
            }
            if (canvasBackgroundImage != null)
            {
                g.DrawImage(canvasBackgroundImage, whole);
            }
            if (image != null)
            {
                g.DrawImage(image, whole);
            }
        }

        const int parallelMinPixels = 128 * 128;

        // Draws the canvas background and the scaled image for the clip in one pass of our own, rather
        // than as separate GDI+ calls. Two reasons:
        // - It has to be quick at full-screen size, and this way the rows can be done in parallel.
        // - GDI+'s nearest-neighbor scaling picks source pixels that shift with the clip at some
        //   scales, so the image would shimmer wherever a small area (like the brush circle) is repainted.
        private unsafe void DrawComposited(Graphics g, Rectangle clip)
        {
            int clipHeight = clip.Height;

            // packed as clip.Width x clip.Height, whatever the array's real size
            if (scaled == null || scaled.Length < clip.Width * clipHeight)
            {
                scaled = new uint[clip.Width * clipHeight];
            }

            if (sourceColumns == null || sourceColumns.Length < clip.Width)
            {
                sourceColumns = new int[clip.Width];
            }

            int imageWidth = surface.Width;
            int imageHeight = surface.Height;
            long canvasWidth = canvasSize.Width;
            long canvasHeight = canvasSize.Height;
            int clipLeft = clip.Left;
            int clipTop = clip.Top;
            int clipWidth = clip.Width;
            int[] columns = sourceColumns;

            // the source pixel under the center of each output pixel, in whole numbers so that it
            // comes out the same no matter where the clip starts
            for (int x = 0; x < clipWidth; ++x)
            {
                columns[x] = (int)((2L * (clipLeft + x) + 1) * imageWidth / (2 * canvasWidth));
            }

            // What shows through transparent pixels: the same checkerboard as CreateCheckerboardTile,
            // with the canvas background color laid over it.
            int cell = Math.Max(4, (int)Math.Round(8 * this.DeviceDpi / 96f));
            uint back = (uint)canvasBackColor.ToArgb();
            uint light = BlendOver(back, back >> 24, 0xFFFFFFFF);
            uint dark = BlendOver(back, back >> 24, 0xFFBFBFBF);

            // The result goes onto g with plain GDI, which knows nothing about g's transform, so work out
            // where the clip is on the device. (Drawing it with GDI+ instead costs far more: for every
            // row it processes the whole width of the source, however narrow the part being drawn.)
            Point[] deviceOrigin = { clip.Location };
            g.TransformPoints(CoordinateSpace.Device, CoordinateSpace.World, deviceOrigin);

            fixed (uint* scaledPixels = scaled)
            {
                // read the surface's memory directly; locking the bitmap that wraps it isn't needed
                // and fails if a paint is ever nested inside another
                {
                    IntPtr srcScan0 = surface.Scan0.Pointer;
                    IntPtr dstScan0 = (IntPtr)scaledPixels;
                    int srcStride = surface.Stride;

                    Action<int> row = y =>
                    {
                        int sourceRow = (int)((2L * (clipTop + y) + 1) * imageHeight / (2 * canvasHeight));
                        uint* srcPixels = (uint*)((byte*)srcScan0 + (long)sourceRow * srcStride);
                        uint* dstPixels = (uint*)dstScan0 + (long)y * clipWidth;
                        int celly = (clipTop + y) / cell;

                        for (int x = 0; x < clipWidth; ++x)
                        {
                            uint pixel = srcPixels[columns[x]];
                            uint alpha = pixel >> 24;

                            if (alpha != 255)
                            {
                                uint under = (((clipLeft + x) / cell + celly) & 1) == 0 ? light : dark;
                                pixel = BlendOver(pixel, alpha, under);
                            }

                            dstPixels[x] = pixel;
                        }
                    };

                    if ((long)clipWidth * clipHeight < parallelMinPixels)
                    {
                        for (int y = 0; y < clipHeight; ++y)
                        {
                            row(y);
                        }
                    }
                    else
                    {
                        System.Threading.Tasks.Parallel.For(0, clipHeight, row);
                    }
                }

                BITMAPINFOHEADER header = new BITMAPINFOHEADER();
                header.biSize = (uint)sizeof(BITMAPINFOHEADER);
                header.biWidth = clipWidth;
                header.biHeight = -clipHeight; // negative means the rows run top to bottom
                header.biPlanes = 1;
                header.biBitCount = 32;

                IntPtr hdc = g.GetHdc();
                try
                {
                    SetDIBitsToDevice(hdc, deviceOrigin[0].X, deviceOrigin[0].Y, (uint)clipWidth, (uint)clipHeight,
                        0, 0, 0, (uint)clipHeight, (IntPtr)scaledPixels, ref header, 0);
                }
                finally
                {
                    g.ReleaseHdc(hdc);
                }
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern int SetDIBitsToDevice(IntPtr hdc, int xDest, int yDest, uint width, uint height,
            int xSrc, int ySrc, uint startScan, uint lines, IntPtr bits, ref BITMAPINFOHEADER info, uint colorUse);

        // color (straight alpha) over an opaque color
        private static uint BlendOver(uint color, uint alpha, uint under)
        {
            if (alpha == 0)
            {
                return under;
            }

            uint inverse = 255 - alpha;
            uint r = (((color >> 16) & 255) * alpha + ((under >> 16) & 255) * inverse + 127) / 255;
            uint g = (((color >> 8) & 255) * alpha + ((under >> 8) & 255) * inverse + 127) / 255;
            uint b = ((color & 255) * alpha + (under & 255) * inverse + 127) / 255;
            return 0xFF000000 | (r << 16) | (g << 8) | b;
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
            InvalidateCanvas();
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
                    if (image != null)
                        image.Dispose();

                    image = surface.CreateAliasedBitmap();
                    UpdateSize();
                }
            }
        }

        public Color CanvasBackColor
        {
            get
            {
                return canvasBackColor;
            }
            set
            {
                canvasBackColor = value;
                InvalidateCanvas();
            }
        }

        public void InvalidateCanvas()
        {
            this.Invalidate(CanvasBounds);
        }

        /// <param name="invalidRect">in the surface's coordinates</param>
        public void InvalidateCanvas(Rectangle invalidRect)
        {
            Point location = CanvasLocation;

            // round outwards, so a partly covered pixel at the edge is included
            int left = (int)Math.Floor(invalidRect.Left * scale);
            int top = (int)Math.Floor(invalidRect.Top * scale);
            int right = (int)Math.Ceiling(invalidRect.Right * scale);
            int bottom = (int)Math.Ceiling(invalidRect.Bottom * scale);

            this.Invalidate(new Rectangle(left + location.X, top + location.Y, right - left, bottom - top));
        }

        private void UpdateSize()
        {
            if (surface != null)
            {
                canvasSize = surface.Size.Factor(scale);
            }

            UpdateScrollbars();
            this.Invalidate();
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

        // The picture moves under the mouse when it scrolls, so work out again where on the canvas the
        // mouse is and put the brush circle there.
        private void SyncBrushToMouse()
        {
            if (!panelhasmouse || !IsHandleCreated)
            {
                return;
            }

            Point mouse = PointToClient(Cursor.Position);
            Point location = CanvasLocation;

            InvalidateBrush();
            canvasmouselocation = new PointF(mouse.X - location.X, mouse.Y - location.Y);
            canvashasmouse = CanvasBounds.Contains(mouse);
            InvalidateBrush();
        }

        // Every way of scrolling ends up here. Paint messages wait behind mouse input, so during a
        // drag the view would lag behind; Update() repaints right away.
        private void AfterScroll()
        {
            SyncBrushToMouse();
            Update();
        }

        private void CanvasPanel_MouseDown(object sender, MouseEventArgs e)
        {
            Point location = CanvasLocation;

            if (e.Button == MouseButtons.Right)
                ShowContextMenu(e.Location, CanvasBounds.Contains(e.Location));

            OnCanvasMouseDown(e.Button, e.X - location.X, e.Y - location.Y);
        }

        // Right-clicking the canvas changes what shows through the image's transparent parts;
        // right-clicking the area around it changes that area's color.
        private void ShowContextMenu(Point location, bool onCanvas)
        {
            Action<Color> setColor = color =>
            {
                if (onCanvas)
                {
                    canvasBackgroundImage = null;
                    canvasBackColor = color;
                }
                else
                {
                    this.BackColor = color;
                }
                this.Invalidate();
            };

            contextMenu.Items.Clear();
            using (Surface sfc = new Surface(16, 16))
            {
                contextMenu.Items.Add(new ToolStripLabel("Background"));
                contextMenu.Items.Add(new ToolStripSeparator());

                if (onCanvas)
                    contextMenu.Items.Add("Transparent", CreateCheckerboardTile(1f), (s, e) => setColor(Color.Transparent));

                sfc.Fill(ColorBgra.Black);
                contextMenu.Items.Add("Black", new Bitmap(sfc.CreateAliasedBitmap()), (s, e) => setColor(Color.Black));

                sfc.Fill(ColorBgra.White);
                contextMenu.Items.Add("White", new Bitmap(sfc.CreateAliasedBitmap()), (s, e) => setColor(Color.White));

                sfc.Fill(ColorBgra.FromBgr(127, 127, 127));
                contextMenu.Items.Add("Gray", new Bitmap(sfc.CreateAliasedBitmap()), (s, e) => setColor(Color.Gray));

                contextMenu.Items.Add("Other color...", new Bitmap(typeof(Liquify),"images.colorwheel.png"), (s, e) =>
                {
                    ColorBgra c;
                    if (DialogResult.OK == ShowColorPicker(onCanvas ? canvasBackColor : this.BackColor, onCanvas, out c))
                    {
                        setColor(c.ToColor());
                    }
                });

                if (onCanvas)
                {
                    contextMenu.Items.Add("From clipboard", null, (s, e) =>
                    {
                        try
                        {
                            canvasBackgroundImage = Clipboard.GetImage();
                            canvasBackColor = Color.Transparent;
                            this.Invalidate();
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
            contextMenu.Show(this, location);
        }

        private DialogResult ShowColorPicker(Color current, bool alpha, out ColorBgra color)
        {
            using (ColorDialog cd = new ColorDialog(alpha))
            {
                cd.Color = ColorBgra.FromColor(current);

                DialogResult result = cd.ShowDialog(this);
                color = cd.Color;
                return result;
            }
        }

        private void CanvasPanel_MouseMove(object sender, MouseEventArgs e)
        {
            Rectangle canvasBounds = CanvasBounds;
            canvashasmouse = canvasBounds.Contains(e.Location);
            OnCanvasMouseMove(e.Button, e.X - canvasBounds.X, e.Y - canvasBounds.Y);
        }

        private void CanvasPanel_MouseUp(object sender, MouseEventArgs e)
        {
            Point location = CanvasLocation;
            OnCanvasMouseUp(e.Button, e.X - location.X, e.Y - location.Y);
        }

        private void CanvasPanel_MouseLeave(object sender, System.EventArgs e)
        {
            InvalidateBrush();
            panelhasmouse = false;
            canvashasmouse = false;
        }

        private void InvalidateBrush()
        {
            Point location = CanvasLocation;
            int scaledbrushradius = (int)(brushRadius * scale);
            this.Invalidate(new Rectangle(
                (int)canvasmouselocation.X - scaledbrushradius + location.X - 2,
                (int)canvasmouselocation.Y - scaledbrushradius + location.Y - 2,
                2 * scaledbrushradius + 5,
                2 * scaledbrushradius + 5));
        }

        private void CanvasPanel_MouseEnter(object sender, System.EventArgs e)
        {
            panelhasmouse = true;
        }

        private void CanvasPanel_Resize(object sender, System.EventArgs e)
        {
            // the canvas is centered when it fits, so it may have moved
            UpdateScrollbars();
            this.Invalidate();
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

                // zooming can move the canvas, so find where the mouse is on it now
                SyncBrushToMouse();

                if (canvashasmouse) //try to keep the mouse over the same virtual location on the document
                    SetScrollLocation(documentmouselocation);
            }
            else if ((ModifierKeys & Keys.Shift) != Keys.None)
            {
                ScrollBy(-e.Delta, 0);
            }
            else
            {
                ScrollBy(0, -e.Delta);
            }

            AfterScroll();
        }

        private void ScrollBy(int dx, int dy)
        {
            ScrollPosition = new Point(scrollOffset.X + dx, scrollOffset.Y + dy);
        }

        private void SetScrollLocation(PointF documentmouselocation)
        {
            PointF desiredcanvasmouselocation = new PointF(documentmouselocation.X * scale, documentmouselocation.Y * scale);
            int dx = (int)(canvasmouselocation.X - desiredcanvasmouselocation.X);
            int dy = (int)(canvasmouselocation.Y - desiredcanvasmouselocation.Y);

            ScrollBy(-dx, -dy);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            //do nothing
            //we don't want the panel scrolling vertically when trying to change zoom or scroll horizontally

            //the owner is responsible for calling PerformMouseWheel()
        }
    }
}
