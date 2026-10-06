using System;
using System.Drawing;
using System.Windows.Forms;

namespace pyrochild.effects.common
{
    public class CanvasMouseEventArgs : EventArgs
    {
        private readonly MouseButtons button;
        private readonly float x, y;
        private readonly float pressure;
        private readonly bool eraser;

        public CanvasMouseEventArgs(MouseButtons button, float x, float y, float pressure = 1f, bool eraser = false)
        {
            this.button = button;
            this.x = x;
            this.y = y;
            this.pressure = pressure;
            this.eraser = eraser;
        }

        /// <summary>
        /// True for a stroke made with the eraser end of a pen. Such a stroke is always reported
        /// with the left button, whichever button the pen's driver sends for it.
        /// </summary>
        public bool Eraser
        {
            get
            {
                return eraser;
            }
        }

        /// <summary>
        /// How hard a pen is being pressed, from 0 to 1. Always 1 for a mouse, and for a pen that
        /// doesn't report pressure.
        /// </summary>
        public float Pressure
        {
            get
            {
                return pressure;
            }
        }

        public MouseButtons Button
        {
            get
            {
                return button;
            }
        }

        public float X
        {
            get
            {
                return x;
            }
        }

        public float Y
        {
            get
            {
                return y;
            }
        }

        public PointF Location
        {
            get
            {
                return new PointF(x, y);
            }
        }
    }
}