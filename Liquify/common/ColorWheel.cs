using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PaintDotNet;

namespace pyrochild.effects.common
{
    public class ColorWheel : Control
    {
        private Bitmap renderBitmap = null;
        private Surface renderSurface = null;
        private bool trackingH = false;
        private bool trackingSV = false;
        private bool hasmouse;
        private Point lastmouse;
        private float radius;
        private float innerradius;
        private float huerad;
        private float theight;
        private float padding;
        private double mouser;

        const float radtodeg = (float)(180 / Math.PI);
        const float thirtydeg = 30 / radtodeg;
        const float ringradiusratio = 0.75f;

        private ColorBgra color;
        private HsvColor hsvcolor;

        public ColorBgra Color
        {
            get
            {
                return color;
            }

            set
            {
                if (color != value)
                {
                    color = value;
                    hsvcolor = HsvColor.FromColor(color.ToColor());
                    OnColorChanged();
                    Invalidate();
                }
            }
        }

        public HsvColor HsvColor
        {
            get
            {
                return hsvcolor;
            }
            set
            {
                if (hsvcolor != value)
                {
                    hsvcolor = value;
                    color = hsvcolor.ToColorBgra();
                    OnColorChanged();
                    Invalidate();
                }
            }
        }

        public ColorWheel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            DoubleBuffered = true;
            ResizeRedraw = true;

            // This call is required by the Windows.Forms Form Designer.
            InitializeComponent();

            color = ColorBgra.Zero;
        }

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        //protected override void OnLoad(EventArgs e)
        //{
        //    InitRendering();
        //    base.OnLoad(e);
        //}

        protected override void OnPaint(PaintEventArgs e)
        {
            InitRendering();

            int length = Math.Min(Width, Height);

            e.Graphics.DrawImage(
                renderBitmap,
                new Rectangle(0, 0, length, length),
                new Rectangle(0, 0, renderBitmap.Width, renderBitmap.Height),
                GraphicsUnit.Pixel);

            if (hasmouse && (mouser <= radius || trackingSV || trackingH))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawLine(Pens.Black,
                    (innerradius * (float)Math.Cos(huerad) + radius),
                    (-innerradius * (float)Math.Sin(huerad) + radius),
                    (radius * (float)Math.Cos(huerad) + radius),
                    (-radius * (float)Math.Sin(huerad)) + radius);

                PointF[] p = GetVertices(radius, huerad);

                float s = hsvcolor.Saturation / 100.0f;
                float v = hsvcolor.Value / 100.0f;

                float hx = p[0].X;
                float hy = p[0].Y;
                float bx = p[1].X;
                float by = p[1].Y;
                float wx = p[2].X;
                float wy = p[2].Y;

                int ix = (int)(bx + (wx - bx) * v + (hx - wx) * s * v + 0.5);
                int iy = (int)(by + (wy - by) * v + (hy - wy) * s * v + 0.5);

                e.Graphics.DrawRectangle(Pens.Black, ix - 1, iy - 1, 3, 3);
                e.Graphics.DrawRectangle(Pens.White, ix, iy, 1, 1);
            }

            base.OnPaint(e);
        }

        private void InitRendering()
        {
            if (renderSurface == null)
            {
                InitRenderSurface();
            }
            DrawWheel(renderSurface);
        }

        private static PointF[] GetVertices(float radius, float hue)
        {
            PointF[] vertices = new PointF[3];
            float r = radius * ringradiusratio;// -padding;

            vertices[0].X = (r * (float)Math.Cos(-hue) + radius);
            vertices[0].Y = (r * (float)Math.Sin(-hue) + radius);
            vertices[1].X = (r * (float)Math.Cos(-hue + Math.PI * 2 / 3) + radius);
            vertices[1].Y = (r * (float)Math.Sin(-hue + Math.PI * 2 / 3) + radius);
            vertices[2].X = (r * (float)Math.Cos(-hue + Math.PI * 4 / 3) + radius);
            vertices[2].Y = (r * (float)Math.Sin(-hue + Math.PI * 4 / 3) + radius);

            return vertices;
        }

        private void InitRenderSurface()
        {
            if (renderSurface != null)
                renderSurface.Dispose();

            int wheelDiameter = Math.Min(Width, Height);
            radius = wheelDiameter / 2;
            innerradius = radius * ringradiusratio;
            theight = innerradius * 1.5f;
            padding = (radius - innerradius) / 4;

            renderSurface = new Surface(Math.Max(1, wheelDiameter),
                                           Math.Max(1, wheelDiameter));
            renderBitmap = renderSurface.CreateAliasedBitmap();
        }

        private unsafe void DrawWheel(Surface sfc)
        {
            huerad = hsvcolor.Hue / radtodeg;

            for (int y = 0; y < sfc.Height; y++)
            {
                ColorBgra* ptr = sfc.GetRowAddress(y);
                float cy = radius - y;
                float cy2 = cy * cy;
                for (int x = 0; x < sfc.Width; x++)
                {
                    float cx = x - radius;

                    double r = Math.Sqrt(cy2 + cx * cx);
                    double theta = Math.Atan2(cy, cx);
                    double tx = r * Math.Cos(theta - huerad) + innerradius / 2;
                    double tu = r * Math.Sin(theta - thirtydeg - huerad) + innerradius / 2;
                    double tv = r * Math.Sin(theta + thirtydeg - huerad) + innerradius;

                    if (theta < 0)
                        theta += 2 * Math.PI;

                    if (r <= radius
                     && r >= innerradius)
                    {
                        //draw the hue ring
                        *ptr = new HsvColor((int)(theta * radtodeg), 100, 100).ToColorBgra();

                        //antialias
                        if (radius - r <= 1 && radius - r >= 0)
                            ptr->A = (byte)(255 * (radius - r));
                        else if (r - innerradius <= 1 && r - innerradius >= 0)
                            ptr->A = (byte)(255 * (r - innerradius));
                    }
                    else if (hasmouse && (mouser <= radius || trackingSV || trackingH))
                    {
                        if (tu >= 0
                          && tv <= theight
                          && tx >= 0)
                        {
                            //draw the triangle
                            float s, v;
                            GetSatVal(cx, cy, out s, out v);

                            int sat = (int)(100 * s);
                            int val = (int)(100 * v);
                            *ptr = new HsvColor(hsvcolor.Hue, sat, val).ToColorBgra();

                            //aa
                            if (tx <= 1 && tx >= 0)
                                ptr->A = (byte)(255 * tx);
                            else if (tu <= 1 && tu >= 0)
                                ptr->A = (byte)(255 * tu);
                            else if (theight - tv <= 1 && theight - tv >= 0)
                                ptr->A = (byte)(255 * (theight - tv));
                        }
                        else if (r <= innerradius - padding &&
                            (tu <= -padding || tx <= -padding || tv >= theight + padding))
                        {
                            //fills the free space between the ring and triangle with the selected color
                            byte v = (byte)((((x ^ y) & 8) * 8) + 191);
                            ptr->Bgra = (uint)v | (uint)(v << 8) | (uint)(v << 16) | 0xff000000;
                            *ptr = UserBlendOps.NormalBlendOp.ApplyStatic(*ptr, color);

                            //aa
                            if (padding + tu >= -1 && padding + tu <= 0)
                                ptr->A = (byte)(255 * (-padding - tu));
                            else if (padding + tx >= -1 && padding + tx <= 0)
                                ptr->A = (byte)(255 * (-padding - tx));
                            else if (tv - theight - padding <= 1 && tv - theight - padding >= 0)
                                ptr->A = (byte)(255 * (tv - theight - padding));

                            if (innerradius - padding - r <= 1 && innerradius - padding - r >= 0)
                                ptr->A = Math.Min(ptr->A, (byte)(255 * (innerradius - padding - r)));
                        }
                        else
                            *ptr = ColorBgra.Transparent;
                    }
                    else if (r <= innerradius - padding)
                    {
                        //fills the free space inside the ring with the selected color
                        byte v = (byte)((((x ^ y) & 8) * 8) + 191);
                        ptr->Bgra = (uint)v | (uint)(v << 8) | (uint)(v << 16) | 0xff000000;
                        *ptr = UserBlendOps.NormalBlendOp.ApplyStatic(*ptr, color);

                        //aa
                        if (innerradius - padding - r <= 1 && innerradius - padding - r >= 0)
                            ptr->A = (byte)(255 * (innerradius - padding - r));
                    }
                    else
                        *ptr = ColorBgra.Transparent;

                    ++ptr;
                }
            }
        }

        private void GetSatVal(float cx, float cy, out float s, out float v)
        {
            PointF[] pts = GetVertices(radius, huerad);

            float hx = pts[0].X - radius;
            float hy = radius - pts[0].Y;
            float sx = pts[1].X - radius;
            float sy = radius - pts[1].Y;
            float vx = pts[2].X - radius;
            float vy = radius - pts[2].Y;


            if (vx * (cx - sx) + vy * (cy - sy) < 0.0f)
            {
                s = 1.0f;
                v = (((cx - sx) * (hx - sx) + (cy - sy) * (hy - sy))
                  / ((hx - sx) * (hx - sx) + (hy - sy) * (hy - sy)));

                if (v < 0.0f)
                    v = 0.0f;
                else if (v > 1.0f)
                    v = 1.0f;
            }
            else if (hx * (cx - sx) + hy * (cy - sy) < 0.0f)
            {
                s = 0.0f;
                v = (((cx - sx) * (vx - sx) + (cy - sy) * (vy - sy))
                  / ((vx - sx) * (vx - sx) + (vy - sy) * (vy - sy)));

                if (v < 0.0f)
                    v = 0.0f;
                else if (v > 1.0f)
                    v = 1.0f;
            }
            else if (sx * (cx - hx) + sy * (cy - hy) < 0.0f)
            {
                v = 1.0f;
                s = (((cx - vx) * (hx - vx) + (cy - vy) * (hy - vy)) /
                  ((hx - vx) * (hx - vx) + (hy - vy) * (hy - vy)));

                if (s < 0.0f)
                    s = 0.0f;
                else if (s > 1.0f)
                    s = 1.0f;
            }
            else
            {
                v = (((cx - sx) * (hy - vy) - (cy - sy) * (hx - vx))
                  / ((vx - sx) * (hy - vy) - (vy - sy) * (hx - vx)));

                if (v <= 0.0f)
                {
                    v = 0.0f;
                    s = 0.0f;
                }
                else
                {
                    if (v > 1.0f)
                        v = 1.0f;

                    if (Math.Abs(hy - vy) < Math.Abs(hx - vx))
                        s = (cx - sx - v * (vx - sx)) / (v * (hx - vx));
                    else
                        s = (cy - sy - v * (vy - sy)) / (v * (hy - vy));

                    if (s < 0.0f)
                        s = 0.0f;
                    else if (s > 1.0f)
                        s = 1.0f;
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (renderSurface != null) // && (ComputeRadius(Size) != ComputeRadius(this.renderSurface.Size)))
            {
                renderSurface.Dispose();
                renderSurface = null;
            }

            Invalidate();
        }

        public event EventHandler ColorChanged;
        private void OnColorChanged()
        {
            if (ColorChanged != null)
            {
                ColorChanged(this, EventArgs.Empty);
            }
        }

        private void GrabHue(Point mouseXY)
        {
            // center our coordinate system so the middle is (0,0), and positive Y is facing up
            float cx = mouseXY.X - radius;
            float cy = radius - mouseXY.Y;

            double theta = Math.Atan2(cy, cx);

            if (theta < 0)
            {
                theta += 2 * Math.PI;
            }

            hsvcolor.Hue = (int)(theta * radtodeg);
            color = hsvcolor.ToColorBgra();
            OnColorChanged();
        }

        private void GrabSatVal(Point e)
        {
            float cy = radius - e.Y;
            float cx = e.X - radius;

            double r = Math.Sqrt(cy * cy + cx * cx);
            double theta = Math.Atan2(cy, cx);
            double tx = r * Math.Cos(theta - huerad) + innerradius / 2;
            double tv = r * Math.Sin(theta + thirtydeg - huerad) + innerradius;
            float s, v;
            GetSatVal(cx, cy, out s, out v);

            int sat = (int)(100 * s);
            int val = (int)(100 * v);

            hsvcolor.Saturation = sat;
            hsvcolor.Value = val;
            color = hsvcolor.ToColorBgra();
            OnColorChanged();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                float cy = radius - e.Y;
                float cx = e.X - radius;

                double r = Math.Sqrt(cy * cy + cx * cx);
                double theta = Math.Atan2(cy, cx);
                double tx = r * Math.Cos(theta - huerad) + innerradius / 2;
                double tu = r * Math.Sin(theta - thirtydeg - huerad) + innerradius / 2;
                double tv = r * Math.Sin(theta + thirtydeg - huerad) + innerradius;

                if (r <= radius
                     && r >= innerradius)
                {
                    trackingH = true;

                    GrabHue(e.Location);
                }
                else if (tu >= 0
                      && tv <= innerradius * 1.5
                      && tx >= 0)
                {
                    trackingSV = true;

                    GrabSatVal(e.Location);
                }
            }

            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            trackingH = trackingSV = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            lastmouse = e.Location;
            mouser = Math.Sqrt((e.X - radius) * (e.X - radius) + (e.Y - radius) * (e.Y - radius));

            if (trackingH)
            {
                GrabHue(e.Location);
            }
            else if (trackingSV)
            {
                GrabSatVal(e.Location);
            }

            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            hasmouse = false;

            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);

            hasmouse = true;

            Invalidate();
        }

        #region Component Designer generated code
        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            SuspendLayout();
            // 
            // ColorWheel
            // 
            Name = "ColorWheel";
            Size = new System.Drawing.Size(167, 156);
            ResumeLayout(false);

        }
        #endregion
    }
}