using pyrochild.effects.common;
using System.Windows.Forms;
using System.Drawing;

namespace pyrochild.effects.liquify
{
    public class LiquifyEventArgs : QueuedToolEventArgs
    {
        private readonly MouseButtons button;
        private readonly int x, y, size;
        private readonly float pressure, density;
        private readonly LiquifyMode mode;

        public LiquifyEventArgs(QueuedToolEventType eventtype, MouseButtons button, int x, int y, int size, float pressure, float density, LiquifyMode mode)
            : base(eventtype)
        {
            this.button = button;
            this.x = x;
            this.y = y;
            this.size = size;
            this.pressure = pressure;
            this.density = density;
            this.mode = mode;
        }

        public MouseButtons Button { get { return button; } }
        public Point Location { get { return new Point(x, y); } }
        public int X { get { return x; } }
        public int Y { get { return y; } }
        public int Size { get { return size; } }
        public float Pressure { get { return pressure; } }
        public float Density { get { return density; } }
        public LiquifyMode Mode { get { return mode; } }
    }
}