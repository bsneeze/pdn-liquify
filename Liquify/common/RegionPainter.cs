using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace pyrochild.effects.common
{
    /// <summary>
    /// Flicker-free painting for a control that can be much bigger than the screen.
    /// WinForms' own double buffering buffers the whole control and paints the bounding box of whatever
    /// needs repainting. This buffers and paints each invalid rectangle on its own instead, so a thin strip
    /// uncovered by scrolling plus a small area elsewhere doesn't turn into repainting everything between them.
    /// </summary>
    internal sealed class RegionPainter : IDisposable
    {
        private const int WM_PAINT = 0x000F;
        private const int maxRects = 16;

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);

        [DllImport("user32.dll")]
        private static extern int GetUpdateRgn(IntPtr hWnd, IntPtr hRgn, bool erase);

        private BufferedGraphicsContext context;
        private Rectangle[] updateRects;

        /// <summary>
        /// Call at the top of the control's WndProc. The invalid region is gone by the time OnPaint
        /// runs, so it has to be picked up here.
        /// </summary>
        public void BeforeWndProc(Control control, ref Message m)
        {
            if (m.Msg != WM_PAINT)
            {
                return;
            }

            updateRects = null;

            IntPtr hrgn = CreateRectRgn(0, 0, 0, 0);
            try
            {
                // 0 is ERROR and 1 is NULLREGION
                if (GetUpdateRgn(control.Handle, hrgn, false) > 1)
                {
                    using (Region region = Region.FromHrgn(hrgn))
                    using (Matrix identity = new Matrix())
                    {
                        RectangleF[] scans = region.GetRegionScans(identity);
                        if (scans.Length > 0 && scans.Length <= maxRects)
                        {
                            updateRects = Array.ConvertAll(scans, Rectangle.Round);
                        }
                    }
                }
            }
            finally
            {
                DeleteObject(hrgn);
            }
        }

        /// <summary>
        /// Call from OnPaint. paint is run once per invalid rectangle, drawing in the control's own
        /// coordinates, and must fill the whole rectangle it is given.
        /// </summary>
        public void Paint(PaintEventArgs pe, Rectangle bounds, Action<Graphics, Rectangle> paint)
        {
            Rectangle[] rects = updateRects;
            updateRects = null;

            Rectangle all = Rectangle.Intersect(pe.ClipRectangle, bounds);
            if (rects == null)
            {
                rects = new Rectangle[] { all };
            }

            if (context == null)
            {
                // our own context, so a buffer of up to screen size is kept between paints
                Size screen = SystemInformation.VirtualScreen.Size;
                context = new BufferedGraphicsContext();
                context.MaximumBuffer = new Size(screen.Width + 1, screen.Height + 1);
            }

            foreach (Rectangle rect in rects)
            {
                Rectangle clip = Rectangle.Intersect(rect, all);
                if (clip.Width <= 0 || clip.Height <= 0)
                {
                    continue;
                }

                using (BufferedGraphics buffered = context.Allocate(pe.Graphics, clip))
                {
                    paint(buffered.Graphics, clip);
                    buffered.Render();
                }
            }
        }

        public void Dispose()
        {
            if (context != null)
            {
                context.Dispose();
                context = null;
            }
        }
    }
}
