using System;
using System.Drawing;
using System.Windows.Forms;

namespace pyrochild.effects.common
{
    public class CanvasMouseEventArgs : EventArgs
    {
        private readonly MouseButtons button;
        private readonly float x, y;

        public CanvasMouseEventArgs(MouseButtons button, float x, float y)
        {
            this.button = button;
            this.x = x;
            this.y = y;
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