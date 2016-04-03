using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using PaintDotNet;

namespace pyrochild.effects.common
{
    class ColorGradientControl
        : Control
    {
        private Point lastmouse = new Point(-1, -1);

        private bool tracking = false;
        private bool highlight = false;

        private const int triangleSize = 7;
        private const int triangleHalfLength = (triangleSize - 1) / 2;

        private ColorBgra[] gradient = null;

        private bool drawNearNub = true;
        public bool DrawNearNub
        {
            get
            {
                return this.drawNearNub;
            }

            set
            {
                this.drawNearNub = value;
                Invalidate();
            }
        }

        private bool drawFarNub = true;
        public bool DrawFarNub
        {
            get
            {
                return this.drawFarNub;
            }

            set
            {
                this.drawFarNub = value;
                Invalidate();
            }
        }

        public ColorBgra[] Gradient
        {
            get
            {
                return this.gradient;
            }

            set
            {
                if (!CompareGradients(value, this.gradient))
                {
                    this.gradient = value;

                    Invalidate();
                }
            }
        }

        private bool CompareGradients(ColorBgra[] lhs, ColorBgra[] rhs)
        {
            if (lhs == null || rhs == null)
                return false;
            for (int i = 0; i < lhs.Length; ++i)
                if (lhs[i] != rhs[i])
                    return false;
            return true;
        }

        private float value;
        public float Value
        {
            get
            {
                return value;
            }
            set
            {
                if (this.value != value)
                {
                    this.value = value;
                    OnValueChanged();
                    Invalidate();
                }
            }
        }

        public event EventHandler ValueChanged;
        private void OnValueChanged()
        {
            if (ValueChanged != null)
            {
                ValueChanged(this, new EventArgs());
            }
        }

        public ColorGradientControl()
        {
            // This call is required by the Windows.Forms Form Designer.
            InitializeComponent();

            this.DoubleBuffered = true;
            this.ResizeRedraw = true;
        }

        private void DrawGradient(Graphics g)
        {
            Rectangle gradientRect;

            // draw gradient
            gradientRect = ClientRectangle;
            gradientRect.Inflate(-triangleHalfLength, -triangleSize + 3);

            if (this.gradient != null && gradientRect.Width > 1 && gradientRect.Height > 1)
            {
                Surface gradientSurface = new Surface(gradientRect.Size);

                using (RenderArgs ra = new RenderArgs(gradientSurface))
                {
                    for (int y = 0; y < gradientSurface.Height; ++y)
                    {
                        for (int x = 0; x < gradientSurface.Width; ++x)
                        {
                            float f = x * (gradient.Length - 1) / (float)gradientRect.Width;
                            int i = (int)f;
                            byte v = (byte)((((x ^ y) & 4) * 16) + 191);
                            ColorBgra checker = ColorBgra.FromUInt32((uint)v | (uint)(v << 8) | (uint)(v << 16) | 0xff000000);
                            ColorBgra c = ColorBgra.Lerp(gradient[i], gradient[i + 1], f - i);

                            gradientSurface[x, y] = UserBlendOps.NormalBlendOp.ApplyStatic(checker, c);
                        }
                    }

                    g.DrawImage(ra.Bitmap, gradientRect, ra.Bounds, GraphicsUnit.Pixel);
                }

                gradientSurface.Dispose();
            }

            // draw value triangles
            Brush brush;
            Pen pen;

            if (highlight)
            {
                brush = Brushes.Blue;
                pen = Pens.White;
            }
            else
            {
                brush = this.Enabled ? Brushes.Black : Brushes.Gray;
                pen = Pens.Gray;
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;

            Point a1;
            Point b1;
            Point c1;

            Point a2;
            Point b2;
            Point c2;

            int pos = gradientRect.Left + (int)(gradientRect.Width * value + 0.5);

            a1 = new Point(pos - triangleHalfLength, 0);
            b1 = new Point(pos, triangleSize - 1);
            c1 = new Point(pos + triangleHalfLength, 0);

            a2 = new Point(a1.X, Height - 1 - a1.Y);
            b2 = new Point(b1.X, Height - 1 - b1.Y);
            c2 = new Point(c1.X, Height - 1 - c1.Y);

            if (this.drawNearNub)
            {
                g.FillPolygon(brush, new Point[] { a1, b1, c1, a1 });
            }

            if (this.drawFarNub)
            {
                g.FillPolygon(brush, new Point[] { a2, b2, c2, a2 });
            }

            if (this.drawNearNub)
            {
                g.DrawPolygon(pen, new Point[] { a1, b1, c1, a1 });
            }

            if (this.drawFarNub)
            {
                g.DrawPolygon(pen, new Point[] { a2, b2, c2, a2 });
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            DrawGradient(e.Graphics);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            DrawBackground(e.Graphics);
        }

        private void DrawBackground(Graphics g)
        {
            //transparency grid?
        }

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            }

            base.Dispose(disposing);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                tracking = true;
                OnMouseMove(e);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButtons.Left)
            {
                tracking = false;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (tracking)
            {
                float val = (e.X - triangleHalfLength) / (float)(Width - triangleSize + 1);
                if (val < 0f) val = 0f;
                if (val > 1f) val = 1f;
                Value = val;
            }

            else if (!highlight)
            {
                highlight = true;
                Invalidate();
            }

            this.lastmouse = e.Location;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            highlight = false;
            Invalidate();
        }

        #region Component Designer generated code
        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
        }
        #endregion
    }
}
