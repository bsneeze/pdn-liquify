using PaintDotNet;
using pyrochild.effects.liquify;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace pyrochild.effects.common
{
    /// <summary>
    /// Draws over one row of the canvas after the image has been drawn into it.
    /// </summary>
    /// <param name="pixels">the row's pixels, 32-bit BGRA and already opaque, starting at canvasX</param>
    /// <param name="canvasX">canvas x coordinate of the first pixel (zoomed pixels, not image pixels)</param>
    /// <param name="canvasY">canvas y coordinate of the row</param>
    /// <param name="count">number of pixels in the row</param>
    /// <param name="scale">the zoom factor: canvas pixels per image pixel</param>
    public delegate void CanvasRowOverlay(IntPtr pixels, int canvasX, int canvasY, int count, float scale);

    /// <summary>
    /// An entry the owner adds to the canvas's "Background" menu.
    /// </summary>
    public sealed class CanvasBackgroundOption
    {
        public CanvasBackgroundOption(string name, Func<Surface> getSurface, Func<Image> getPreview = null)
        {
            Name = name;
            GetSurface = getSurface;
            GetPreview = getPreview;
            Enabled = true;
        }

        /// <summary>
        /// An entry that is listed but can't be picked, so the menu can say why something isn't on offer.
        /// </summary>
        public static CanvasBackgroundOption Unavailable(string name)
        {
            return new CanvasBackgroundOption(name, null) { Enabled = false };
        }

        public bool Enabled { get; private set; }

        /// <summary>
        /// Optional. Called each time the menu opens, for the small picture next to the entry, like
        /// the color swatches the built-in entries have. The image stays the caller's.
        /// </summary>
        public Func<Image> GetPreview { get; private set; }

        /// <summary>The menu text.</summary>
        public string Name { get; private set; }

        /// <summary>
        /// Called when the entry is picked. Returns a surface the size of the canvas's own, which
        /// stays the caller's to dispose.
        /// </summary>
        public Func<Surface> GetSurface { get; private set; }
    }

    /// <summary>
    /// A scrollable, zoomable view of a Surface with a brush cursor, which raises mouse events in the
    /// surface's coordinates.
    /// The image ("the canvas") is drawn straight onto this control, and scrolling is done here too:
    /// a scroll step just changes ScrollPosition and repaints. ScrollableControl's AutoScroll isn't
    /// used because it scrolls by copying the window's pixels, which at large window sizes was measured
    /// to take about twice as long as repainting the whole view.
    /// </summary>
    public partial class CanvasPanel : UserControl, IDarkThemeable
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
        private CanvasRowOverlay rowOverlay;
        private Surface backgroundSurface;
        private CanvasBackgroundOption activeBackgroundOption;
        private Size menuSwatchSize; // the menu's own image size, before it is widened for the check mark
        private readonly List<CanvasBackgroundOption> backgroundOptions = new List<CanvasBackgroundOption>();
        private uint[] scaled; // scratch pixels for DrawComposited
        private int[] sourceColumns;

        public CanvasPanel()
        {
            InitializeComponent();

            // Painting is buffered by RegionPainter. Not DoubleBuffered: that allocates a buffer the size
            // of the whole panel on every paint.
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

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

        private Point scrollOffset; // how far the view is scrolled; never negative
        private Size viewport;      // the client area; the scrollbars are outside it
        private bool updatingScrollbars;
        private bool showHScroll;
        private bool showVScroll;

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
                    this.Invalidate();
                }
            }
        }

        // The scrollbars are the window's own (WS_HSCROLL / WS_VSCROLL), driven directly with
        // SetScrollInfo. They have to be the standard kind: touchpad drivers that do their own
        // two-finger scrolling (Synaptics) look for exactly these and send the window WM_HSCROLL and
        // WM_VSCROLL. With scrollbar child controls instead, such a driver sent nothing at all for a
        // sideways swipe.
        private const int SB_HORZ = 0;
        private const int SB_VERT = 1;
        private const uint SIF_RANGE = 0x1;
        private const uint SIF_PAGE = 0x2;
        private const uint SIF_POS = 0x4;
        private const uint SIF_TRACKPOS = 0x10;
        private const int WS_HSCROLL = 0x00100000;
        private const int WS_VSCROLL = 0x00200000;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct SCROLLINFO
        {
            public uint cbSize;
            public uint fMask;
            public int nMin;
            public int nMax;
            public uint nPage;
            public int nPos;
            public int nTrackPos;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetScrollInfo(IntPtr hWnd, int bar, ref SCROLLINFO info, bool redraw);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetScrollInfo(IntPtr hWnd, int bar, ref SCROLLINFO info);

        protected override CreateParams CreateParams
        {
            get
            {
                // so the bars survive if WinForms rebuilds the window's styles
                CreateParams cp = base.CreateParams;
                if (showHScroll) cp.Style |= WS_HSCROLL;
                if (showVScroll) cp.Style |= WS_VSCROLL;
                return cp;
            }
        }

        // Brings the scrollbars and scrollOffset in line with the canvas size and each other.
        private void UpdateScrollbars()
        {
            int extentWidth = canvasSize.Width + 2 * canvasMargin;
            int extentHeight = canvasSize.Height + 2 * canvasMargin;

            if (IsHandleCreated && !updatingScrollbars)
            {
                // setting a bar can change the client size, which raises Resize, which comes back here
                updatingScrollbars = true;
                try
                {
                    // Windows shows a bar only when its page is smaller than its range. Showing one
                    // shrinks the client area, which can make the other one necessary, so go round
                    // more than once.
                    for (int pass = 0; pass < 3; ++pass)
                    {
                        scrollOffset = ClampScroll(scrollOffset, ClientSize, extentWidth, extentHeight);
                        SetScrollBar(SB_HORZ, extentWidth, ClientSize.Width, scrollOffset.X);
                        SetScrollBar(SB_VERT, extentHeight, ClientSize.Height, scrollOffset.Y);
                    }
                }
                finally
                {
                    updatingScrollbars = false;
                }
            }

            viewport = ClientSize;
            scrollOffset = ClampScroll(scrollOffset, viewport, extentWidth, extentHeight);
            showHScroll = extentWidth > viewport.Width;
            showVScroll = extentHeight > viewport.Height;
        }

        private static Point ClampScroll(Point offset, Size view, int extentWidth, int extentHeight)
        {
            return new Point(
                Math.Max(0, Math.Min(offset.X, extentWidth - view.Width)),
                Math.Max(0, Math.Min(offset.Y, extentHeight - view.Height)));
        }

        // what each bar was last set to: redrawing a scrollbar is slow enough to matter when it
        // happens several times per scroll step, so only real changes are passed on
        private readonly int[] barExtent = { -1, -1 };
        private readonly int[] barPage = { -1, -1 };
        private readonly int[] barPosition = { -1, -1 };

        private void SetScrollBar(int bar, int extent, int page, int position)
        {
            if (barExtent[bar] == extent && barPage[bar] == page && barPosition[bar] == position)
            {
                return;
            }

            barExtent[bar] = extent;
            barPage[bar] = page;
            barPosition[bar] = position;

            SCROLLINFO info = new SCROLLINFO();
            info.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(SCROLLINFO));
            info.fMask = SIF_RANGE | SIF_PAGE | SIF_POS;
            info.nMin = 0;
            info.nMax = Math.Max(0, extent - 1);
            info.nPage = (uint)Math.Max(0, page);
            info.nPos = position;
            SetScrollInfo(this.Handle, bar, ref info, true);
        }

        // Where a WM_HSCROLL or WM_VSCROLL command wants the view to be, or -1 for no change.
        // These come from the scrollbars themselves and from touchpad drivers.
        private int ScrollCommandTarget(bool horizontal, IntPtr wParam)
        {
            int command = (int)((long)wParam & 0xFFFF);
            int current = horizontal ? scrollOffset.X : scrollOffset.Y;
            int page = horizontal ? viewport.Width : viewport.Height;
            int line = Math.Max(1, page / 20);

            switch (command)
            {
                case 0: return Math.Max(0, current - line);   // SB_LINEUP / SB_LINELEFT
                case 1: return current + line;                // SB_LINEDOWN / SB_LINERIGHT
                case 2: return Math.Max(0, current - page);   // SB_PAGEUP / SB_PAGELEFT
                case 3: return current + page;                // SB_PAGEDOWN / SB_PAGERIGHT
                case 6: return 0;                             // SB_TOP / SB_LEFT
                case 7: return int.MaxValue / 2;              // SB_BOTTOM / SB_RIGHT

                case 4:                                       // SB_THUMBPOSITION
                case 5:                                       // SB_THUMBTRACK
                    {
                        // The message only carries 16 bits of the position. While the thumb is really
                        // being dragged the full value is in nTrackPos; a message a driver made up
                        // doesn't update that, so its 16 bits are all there is.
                        int carried = (int)(((long)wParam >> 16) & 0xFFFF);

                        SCROLLINFO info = new SCROLLINFO();
                        info.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(SCROLLINFO));
                        info.fMask = SIF_TRACKPOS;
                        if (GetScrollInfo(this.Handle, horizontal ? SB_HORZ : SB_VERT, ref info) && (info.nTrackPos & 0xFFFF) == carried)
                        {
                            return info.nTrackPos;
                        }

                        return carried;
                    }

                default: return -1;                           // SB_ENDSCROLL
            }
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

        private const int WM_HSCROLL = 0x0114;
        private const int WM_VSCROLL = 0x0115;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_MOUSEHWHEEL = 0x020E;

        protected override void WndProc(ref Message m)
        {

            if (m.Msg == WM_MOUSEHWHEEL)
            {
                HandleWheelMessage(ref m);
                return;
            }

            // from the window's own scrollbars, or from a touchpad driver that scrolls with them
            if (m.Msg == WM_HSCROLL || m.Msg == WM_VSCROLL)
            {
                bool horizontal = m.Msg == WM_HSCROLL;
                int target = ScrollCommandTarget(horizontal, m.WParam);
                if (target >= 0)
                {
                    ScrollPosition = horizontal ? new Point(target, scrollOffset.Y) : new Point(scrollOffset.X, target);
                    AfterScroll(false);
                }
                m.Result = IntPtr.Zero;
                return;
            }

            painter.BeforeWndProc(this, ref m);
            base.WndProc(ref m);
        }

        /// <summary>
        /// The wheel distance in a WM_MOUSEWHEEL or WM_MOUSEHWHEEL message. For the horizontal one,
        /// positive is to the right.
        /// </summary>
        public static int WheelDelta(Message m)
        {
            return (short)(((long)m.WParam >> 16) & 0xFFFF);
        }

        /// <summary>
        /// Scrolls for a wheel message, whichever window it was addressed to.
        /// </summary>
        internal void HandleWheelMessage(ref Message m)
        {
            if (m.Msg == WM_MOUSEHWHEEL)
            {
                PerformHorizontalMouseWheel(WheelDelta(m));

                // The documentation says to return 0, but some mouse and touchpad drivers take that
                // as "not handled" and switch to emulating the scroll some other way.
                m.Result = (IntPtr)1;
            }
            else
            {
                PerformMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 0, 0, WheelDelta(m)));
                m.Result = IntPtr.Zero;
            }
        }

        // Whether a wheel message, whoever it is addressed to, is meant for the canvas.
        private bool IsMouseOverForWheel()
        {
            if (!IsHandleCreated || !Visible)
            {
                return false;
            }

            Form form = FindForm();
            if (form == null || Form.ActiveForm != form)
            {
                return false;
            }

            return RectangleToScreen(ClientRectangle).Contains(Cursor.Position);
        }

        /// <summary>
        /// Scrolls sideways for a horizontal wheel or a two-finger sideways swipe on a touchpad. WinForms
        /// has no event for these, so the owner passes on WM_MOUSEHWHEEL messages that reach it instead
        /// of this control, the same way it calls PerformMouseWheel.
        /// </summary>
        public void PerformHorizontalMouseWheel(int delta)
        {
            ScrollBy(delta, 0);
            AfterScroll(false);
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

            painter.Paint(e, ClientRectangle, (g, clip) =>
            {
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

                if (panelhasmouse && !panning && !spaceHeld && !panMode)
                {
                    int scaledbrushradius = (int)(brushRadius * scale);
                    int left = (int)canvasmouselocation.X - scaledbrushradius - 1 + canvasBounds.X;
                    int top = (int)canvasmouselocation.Y - scaledbrushradius - 1 + canvasBounds.Y;
                    int diameter = 2 * scaledbrushradius + 2;

                    // a black ring with a white one just inside it, so it shows on dark and light images alike
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawEllipse(Pens.Black, left, top, diameter, diameter);
                    if (diameter > 4)
                    {
                        g.DrawEllipse(Pens.White, left + 1, top + 1, diameter - 2, diameter - 2);
                    }
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
            CanvasRowOverlay overlay = rowOverlay;
            float overlayScale = scale;

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

                    // the optional image behind the canvas's own, pixel for pixel
                    Surface background = backgroundSurface;
                    if (background != null && (background.IsDisposed || background.Size != surface.Size))
                    {
                        background = null;
                    }
                    IntPtr backScan0 = background != null ? background.Scan0.Pointer : IntPtr.Zero;
                    int backStride = background != null ? background.Stride : 0;

                    Action<int> row = y =>
                    {
                        int sourceRow = (int)((2L * (clipTop + y) + 1) * imageHeight / (2 * canvasHeight));
                        uint* srcPixels = (uint*)((byte*)srcScan0 + (long)sourceRow * srcStride);
                        uint* backPixels = backScan0 != IntPtr.Zero ? (uint*)((byte*)backScan0 + (long)sourceRow * backStride) : null;
                        uint* dstPixels = (uint*)dstScan0 + (long)y * clipWidth;
                        int celly = (clipTop + y) / cell;

                        for (int x = 0; x < clipWidth; ++x)
                        {
                            uint pixel = srcPixels[columns[x]];
                            uint alpha = pixel >> 24;

                            if (alpha != 255)
                            {
                                uint under = (((clipLeft + x) / cell + celly) & 1) == 0 ? light : dark;

                                if (backPixels != null)
                                {
                                    uint back = backPixels[columns[x]];
                                    under = BlendOver(back, back >> 24, under);
                                }

                                pixel = BlendOver(pixel, alpha, under);
                            }

                            dstPixels[x] = pixel;
                        }

                        if (overlay != null)
                        {
                            overlay((IntPtr)dstPixels, clipLeft, clipTop + y, clipWidth, overlayScale);
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

        /// <summary>
        /// Optional. An image the same size as Surface that shows through wherever Surface is
        /// transparent, in place of the checkerboard (which still shows where this is transparent too).
        /// The canvas does not take ownership of it. Setting it replaces any background color or image
        /// picked from the menu.
        /// </summary>
        public Surface BackgroundSurface
        {
            get
            {
                return backgroundSurface;
            }
            set
            {
                bool changed = backgroundSurface != value;

                backgroundSurface = value;
                activeBackgroundOption = null; // SelectBackgroundOption sets it again afterwards
                if (value != null)
                {
                    canvasBackgroundImage = null;
                    canvasBackColor = Color.Transparent;
                }
                InvalidateCanvas();

                if (changed && BackgroundSurfaceChanged != null)
                {
                    BackgroundSurfaceChanged(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Raised when BackgroundSurface changes, including when the user replaces it by picking a
        /// color or image from the menu. An owner can use it to free a surface that is no longer shown.
        /// </summary>
        public event EventHandler BackgroundSurfaceChanged;

        /// <summary>
        /// Extra entries for the canvas's right-click "Background" menu. Picking one makes its surface
        /// the BackgroundSurface.
        /// </summary>
        public IList<CanvasBackgroundOption> BackgroundOptions
        {
            get { return backgroundOptions; }
        }

        /// <summary>
        /// Optional. Called for every row of the canvas as it is painted, to draw over the image at
        /// screen resolution. It is called from several threads at once.
        /// </summary>
        public CanvasRowOverlay RowOverlay
        {
            get
            {
                return rowOverlay;
            }
            set
            {
                rowOverlay = value;
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

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetKeyState(int virtualKey);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        private const int VK_SPACE = 0x20;

        private bool panMode;

        /// <summary>
        /// While set, dragging with the left button pans instead of raising canvas mouse events, with
        /// no key held. Some laptops ignore their touchpad and its buttons while a key is down, which
        /// makes space-drag impossible on them.
        /// </summary>
        public bool PanMode
        {
            get
            {
                return panMode;
            }
            set
            {
                if (panMode != value)
                {
                    InvalidateBrush();
                    panMode = value;
                    UpdatePanCursor();
                }
            }
        }

        private bool panning;
        private MouseButtons panButton;
        private Point panStartMouse;
        private Point panStartScroll;

        // Dragging with the middle button, or with the left button while space is held, scrolls the view.
        private bool BeginPan(MouseButtons button)
        {
            if (panning)
            {
                return true;
            }

            // either view of the keyboard will do: the state as of the message being handled, or right now
            bool spaceDown = GetKeyState(VK_SPACE) < 0 || GetAsyncKeyState(VK_SPACE) < 0;
            bool pan = button == MouseButtons.Middle || (button == MouseButtons.Left && (panMode || spaceDown));


            if (!pan)
            {
                return false;
            }

            InvalidateBrush(); // the brush circle is hidden while panning
            panning = true;
            panButton = button;
            panStartMouse = Cursor.Position;
            panStartScroll = ScrollPosition;
            UpdatePanCursor();
            return true;
        }

        // While space is held the panel is ready to pan: it shows the pan cursor instead of the brush
        // circle, before any dragging starts. This control never has keyboard focus, so it watches
        // for the key with a message filter.
        private bool spaceHeld;
        private InputFilter inputFilter;

        private sealed class InputFilter : IMessageFilter
        {
            private const int WM_KEYDOWN = 0x0100;
            private const int WM_KEYUP = 0x0101;

            private readonly CanvasPanel owner;

            public InputFilter(CanvasPanel owner)
            {
                this.owner = owner;
            }

            public bool PreFilterMessage(ref Message m)
            {
                // Wheel messages go to whichever control has focus, or to whatever the touchpad
                // driver picks, which is rarely this one. Take them here while the mouse is over
                // the canvas, so scrolling it doesn't depend on any of that.
                if ((m.Msg == WM_MOUSEWHEEL || m.Msg == WM_MOUSEHWHEEL) && owner.IsMouseOverForWheel())
                {
                    owner.HandleWheelMessage(ref m);
                    return true;
                }

                if ((m.Msg == WM_KEYDOWN || m.Msg == WM_KEYUP) && (int)m.WParam == VK_SPACE)
                {
                    bool down = m.Msg == WM_KEYDOWN;

                    // a space typed into a text field is just a space
                    Control target = down ? Control.FromChildHandle(m.HWnd) : null;
                    if (!(target is TextBoxBase || target is ComboBox))
                    {
                        owner.SetSpaceHeld(down);
                    }
                }

                // only watching: the message carries on as usual
                return false;
            }
        }

        // Windows' own dark scrollbars, the ones Explorer uses in dark mode.
        [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);

        private bool darkScrollbars;

        void IDarkThemeable.ApplyDarkTheme(Color back, Color fore, Color field, Color border)
        {
            darkScrollbars = true;
            if (IsHandleCreated)
            {
                SetWindowTheme(this.Handle, "DarkMode_Explorer", null);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (darkScrollbars)
            {
                SetWindowTheme(this.Handle, "DarkMode_Explorer", null);
            }

            // the scrollbars couldn't be set up before the window existed, and a new window has none
            barExtent[0] = barExtent[1] = -1;
            UpdateScrollbars();

            if (inputFilter == null)
            {
                inputFilter = new InputFilter(this);
                Application.AddMessageFilter(inputFilter);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (inputFilter != null)
            {
                Application.RemoveMessageFilter(inputFilter);
                inputFilter = null;
            }

            base.OnHandleDestroyed(e);
        }

        private void SetSpaceHeld(bool held)
        {
            if (spaceHeld == held)
            {
                return;
            }


            InvalidateBrush();
            spaceHeld = held;
            UpdatePanCursor();
        }

        private void UpdatePanCursor()
        {
            this.Cursor = (panning || spaceHeld || panMode) ? Cursors.SizeAll : Cursors.Default;
        }

        private void ContinuePan()
        {
            Point mouse = Cursor.Position;
            ScrollPosition = new Point(
                panStartScroll.X - (mouse.X - panStartMouse.X),
                panStartScroll.Y - (mouse.Y - panStartMouse.Y));
            AfterScroll(true);
        }

        private void EndPan(MouseButtons button)
        {
            if (button == panButton)
            {
                panning = false;
                UpdatePanCursor();
                SyncBrushToMouse();
            }
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

        // Every way of scrolling ends up here.
        // Paint messages wait behind input, so the view would lag behind unless it is repainted right
        // away. For wheel scrolling (repaintNow false) that is only done once no more wheel messages
        // are waiting: a diagonal touchpad swipe arrives as separate sideways and vertical messages,
        // and painting between the two shows as a staircase.
        private void AfterScroll(bool repaintNow)
        {
            if (!panning)
            {
                SyncBrushToMouse();
            }

            if (repaintNow || !IsWheelMessageQueued())
            {
                Update();
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct NativeMessage
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int x;
            public int y;
            public uint extra;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool PeekMessage(out NativeMessage message, IntPtr hWnd, uint filterMin, uint filterMax, uint remove);

        // looks without removing; WM_MOUSEWHEEL to WM_MOUSEHWHEEL covers both
        private static bool IsWheelMessageQueued()
        {
            NativeMessage message;
            return PeekMessage(out message, IntPtr.Zero, WM_MOUSEWHEEL, WM_MOUSEHWHEEL, 0);
        }

        /// <summary>
        /// Picks the largest zoom level, up to 100%, that shows the whole image.
        /// </summary>
        public void ZoomToFit()
        {
            if (surface == null)
            {
                return;
            }

            int margin = (int)Math.Ceiling(12 * this.DeviceDpi / 96f);
            float fit = Math.Min(
                (this.Width - 2 * margin) / (float)surface.Width,
                (this.Height - 2 * margin) / (float)surface.Height);

            float best = ZoomFactors[0];
            foreach (float factor in ZoomFactors)
            {
                if (factor <= fit && factor <= 1)
                {
                    best = factor;
                }
            }

            ZoomFactor = best;
        }

        // Releases of the button that started a drag are acted on a moment later, and dropped if the
        // same button goes down again first. With two pointing devices in play (a laptop's touchpad
        // and pointing stick, say) Windows can report a held button as a rapid series of releases and
        // presses, which would otherwise chop one stroke into many.
        private const int releaseDelay = 50;
        private Timer releaseTimer;
        private bool releasePending;
        private MouseButtons releaseButton;
        private Point releaseLocation;

        private void DeferRelease(MouseButtons button, Point location)
        {
            bool endsDrag = panning ? button == panButton : (button == buttons && buttons != MouseButtons.None);
            if (!endsDrag)
            {
                Release(button, location);
                return;
            }

            if (releaseTimer == null)
            {
                releaseTimer = new Timer(components);
                releaseTimer.Interval = releaseDelay;
                releaseTimer.Tick += (s, e) => FlushRelease();
            }

            releasePending = true;
            releaseButton = button;
            releaseLocation = location;
            releaseTimer.Stop();
            releaseTimer.Start();
        }

        private void FlushRelease()
        {
            if (releaseTimer != null)
            {
                releaseTimer.Stop();
            }

            if (releasePending)
            {
                releasePending = false;
                Release(releaseButton, releaseLocation);
            }
        }

        private void Release(MouseButtons button, Point location)
        {
            if (panning)
            {
                EndPan(button);
                return;
            }

            Point canvasLocation = CanvasLocation;
            OnCanvasMouseUp(button, location.X - canvasLocation.X, location.Y - canvasLocation.Y);
        }

        // Losing the mouse capture mid-drag (Alt+Tab, a menu opening) means the release will never
        // arrive here, so treat it as one.
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);

            if (!Capture && !releasePending && (panning || buttons != MouseButtons.None))
            {
                DeferRelease(panning ? panButton : buttons, PointToClient(Cursor.Position));
            }
        }

        private void CanvasPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (releasePending)
            {
                if (e.Button == releaseButton)
                {
                    // pressed again before the release was acted on: carry on with the same drag
                    releasePending = false;
                    releaseTimer.Stop();
                    return;
                }

                FlushRelease();
            }

            if (BeginPan(e.Button))
                return;

            Point location = CanvasLocation;

            if (e.Button == MouseButtons.Right)
                ShowContextMenu(e.Location, CanvasBounds.Contains(e.Location));

            OnCanvasMouseDown(e.Button, e.X - location.X, e.Y - location.Y);
        }

        // Right-clicking the canvas changes what shows through the image's transparent parts;
        // right-clicking the area around it changes that area's color. The entry that matches the
        // current background is checked.
        private void ShowContextMenu(Point location, bool onCanvas)
        {
            Action<Color> setColor = color =>
            {
                if (onCanvas)
                {
                    canvasBackgroundImage = null;
                    BackgroundSurface = null;
                    canvasBackColor = color;
                }
                else
                {
                    this.BackColor = color;
                }
                this.Invalidate();
            };

            // what the background is now, to check the matching entry
            Color current = onCanvas ? canvasBackColor : this.BackColor;
            bool plainColor = !onCanvas || (canvasBackgroundImage == null && backgroundSurface == null);
            bool isTransparent = plainColor && current.A == 0;
            bool isBlack = plainColor && current.ToArgb() == Color.Black.ToArgb();
            bool isWhite = plainColor && current.ToArgb() == Color.White.ToArgb();
            // the panel's default gray is 127 and the menu's is 128
            bool isGray = plainColor && current.A == 255 && current.R == current.G && current.G == current.B && (current.R == 127 || current.R == 128);
            bool isOtherColor = plainColor && !isTransparent && !isBlack && !isWhite && !isGray;

            // Each entry's image is a check mark space followed by its swatch, drawn as one picture, and
            // the menu's image column is made wide enough for both. The built-in ways of showing a
            // check don't work here: on an entry with an image it is only a faint frame, and a separate
            // check column pushes the swatches out of the shaded strip at the menu's edge.
            if (menuSwatchSize.IsEmpty)
            {
                menuSwatchSize = contextMenu.ImageScalingSize; // already scaled for the screen's DPI
            }
            int swatch = menuSwatchSize.Height;
            int gap = Math.Max(2, swatch / 8);
            Size entryImageSize = new Size(swatch + gap + swatch, swatch);
            contextMenu.ImageScalingSize = entryImageSize;

            Func<string, Image, bool, EventHandler, ToolStripMenuItem> add = (text, image, isCurrent, onClick) =>
            {
                Bitmap entryImage = null;
                if (image != null || isCurrent)
                {
                    entryImage = new Bitmap(entryImageSize.Width, entryImageSize.Height);
                    using (Graphics g = Graphics.FromImage(entryImage))
                    {
                        if (isCurrent)
                        {
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            using (Pen pen = new Pen(SystemColors.MenuText, Math.Max(1.6f, swatch / 9f)))
                            {
                                pen.StartCap = LineCap.Round;
                                pen.EndCap = LineCap.Round;
                                pen.LineJoin = LineJoin.Round;
                                g.DrawLines(pen, new PointF[]
                                {
                                    new PointF(swatch * 0.20f, swatch * 0.52f),
                                    new PointF(swatch * 0.42f, swatch * 0.74f),
                                    new PointF(swatch * 0.82f, swatch * 0.26f)
                                });
                            }
                        }

                        if (image != null)
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(image, swatch + gap, 0, swatch, swatch);
                        }
                    }
                }

                ToolStripMenuItem item = new ToolStripMenuItem(text, entryImage, onClick);
                contextMenu.Items.Add(item);
                return item;
            };

            contextMenu.Items.Clear();
            using (Surface sfc = new Surface(16, 16))
            {
                contextMenu.Items.Add(new ToolStripLabel("Background"));
                contextMenu.Items.Add(new ToolStripSeparator());

                if (onCanvas)
                    add("Transparent", CreateCheckerboardTile(1f), isTransparent, (s, e) => setColor(Color.Transparent));

                sfc.Fill(ColorBgra.Black);
                add("Black", new Bitmap(sfc.CreateAliasedBitmap()), isBlack, (s, e) => setColor(Color.Black));

                sfc.Fill(ColorBgra.White);
                add("White", new Bitmap(sfc.CreateAliasedBitmap()), isWhite, (s, e) => setColor(Color.White));

                sfc.Fill(ColorBgra.FromBgr(127, 127, 127));
                add("Gray", new Bitmap(sfc.CreateAliasedBitmap()), isGray, (s, e) => setColor(Color.Gray));

                add("Other color...", new Bitmap(typeof(Liquify),"images.colorwheel.png"), isOtherColor, (s, e) =>
                {
                    ColorBgra c;
                    if (DialogResult.OK == ShowColorPicker(onCanvas ? canvasBackColor : this.BackColor, onCanvas, out c))
                    {
                        setColor(c.ToColor());
                    }
                });

                if (onCanvas)
                {
                    Image clipboardSwatch = null;
                    if (Clipboard.ContainsImage())
                    {
                        using (Surface fromcb = Surface.CopyFromBitmap((Bitmap)Clipboard.GetImage()))
                        {
                            sfc.FitSurface(ResamplingAlgorithm.SuperSampling, fromcb);
                            clipboardSwatch = new Bitmap(sfc.CreateAliasedBitmap());
                        }
                    }

                    ToolStripMenuItem fromClipboard = add("From clipboard", clipboardSwatch, canvasBackgroundImage != null, (s, e) =>
                    {
                        try
                        {
                            canvasBackgroundImage = Clipboard.GetImage();
                            BackgroundSurface = null;
                            canvasBackColor = Color.Transparent;
                            this.Invalidate();
                        }
                        catch { }
                    });
                    fromClipboard.Enabled = clipboardSwatch != null;

                    // whatever else the owner offers, such as the layers under the one being edited
                    foreach (CanvasBackgroundOption option in backgroundOptions)
                    {
                        CanvasBackgroundOption chosen = option;

                        Image preview = null;
                        if (chosen.Enabled && chosen.GetPreview != null)
                        {
                            // a missing picture shouldn't cost the user the menu
                            try { preview = chosen.GetPreview(); } catch { }
                        }

                        ToolStripMenuItem item = add(chosen.Name, preview, ActiveBackgroundOption == chosen, (s, e) => SelectBackgroundOption(chosen));
                        item.Enabled = chosen.Enabled;
                    }
                }
            }
            contextMenu.Show(this, location);
        }

        /// <summary>
        /// The entry from BackgroundOptions whose surface is the current background, if any.
        /// </summary>
        public CanvasBackgroundOption ActiveBackgroundOption
        {
            get { return backgroundSurface != null ? activeBackgroundOption : null; }
        }

        /// <summary>
        /// Makes one of the BackgroundOptions the background, as picking it from the menu does.
        /// </summary>
        public void SelectBackgroundOption(CanvasBackgroundOption option)
        {
            if (option == null || !option.Enabled)
            {
                return;
            }

            Cursor previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                BackgroundSurface = option.GetSurface();
                activeBackgroundOption = option;
            }
            finally
            {
                Cursor.Current = previous;
            }
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
            if (panning)
            {
                ContinuePan();
                return;
            }

            // While a button we were told went down is still down as far as we know, report that one:
            // a move that comes from a second pointing device doesn't carry the first one's buttons.
            MouseButtons button = buttons != MouseButtons.None ? buttons : e.Button;

            Rectangle canvasBounds = CanvasBounds;
            canvashasmouse = canvasBounds.Contains(e.Location);
            OnCanvasMouseMove(button, e.X - canvasBounds.X, e.Y - canvasBounds.Y);
        }

        private void CanvasPanel_MouseUp(object sender, MouseEventArgs e)
        {
            DeferRelease(e.Button, e.Location);
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

            // a key release can be missed while another window has focus, so check the real state
            SetSpaceHeld(GetKeyState(VK_SPACE) < 0);
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

            AfterScroll(false);
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
