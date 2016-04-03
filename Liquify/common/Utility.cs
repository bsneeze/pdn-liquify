using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pyrochild.effects.common
{
    internal static class Utility
    {
        public static float Distance(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static PointF Lerp(PointF a, PointF b, float t)
        {
            float x = a.X + t * (b.X - a.X);
            float y = a.Y + t * (b.Y - a.Y);
            return new PointF(x, y);
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }
    }
}
