using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using PaintDotNet;
using System.Drawing.Drawing2D;

namespace pyrochild.effects.common
{
    internal static class Extensions
    {
        public static PdnRegion GetOutline(this PdnRegion region, RectangleF bounds, float scalefactor)
        {
            GraphicsPath path = new GraphicsPath();

            PdnRegion region2 = region.Clone();

            Matrix scalematrix = new Matrix(
                bounds,
                new PointF[]{
                    new PointF(bounds.Left, bounds.Top),
                    new PointF(bounds.Right*scalefactor, bounds.Top),
                    new PointF(bounds.Left, bounds.Bottom*scalefactor)
                });
            region2.Transform(scalematrix);

            foreach (RectangleF rect in region2.GetRegionScans())
            {
                path.AddRectangle(RectangleF.Inflate(rect, 1, 1));
            }

            PdnRegion retval = new PdnRegion(path);
            retval.Exclude(region2);

            return retval;
        }

        public static Size Factor(this Size me, float f)
        {
            return new Size((int)(me.Width * f), (int)(me.Height * f));
        }

        public static Rectangle Factor(this Rectangle me, float f)
        {
            return new Rectangle(
                (int)(me.X * f),
                (int)(me.Y * f),
                (int)(me.Width * f),
                (int)(me.Height * f));
        }

        public unsafe static string ToString(this byte[] bytes, Encoding encoding, int startIndex, int length)
        {
            if (length > 0)
            {
                fixed (byte* ptr = &bytes[0])
                {
                    return new string((sbyte*)ptr, startIndex, length, encoding);
                }
            }
            else { return ""; }
        }

        public static string ToString(this byte[] bytes, Encoding encoding)
        {
            return ToString(bytes, encoding, 0, bytes.Length);
        }

        public static byte ClampToByte(this float val)
        {
            if (val < 0) return 0;
            if (val > 255) return 255;
            return (byte)val;
        }

        public static ColorBgra ToColorBgra(this HsvColor color)
        {
            // HsvColor contains values scaled as in the color wheel:

            double h;
            double s;
            double v;

            double r = 0;
            double g = 0;
            double b = 0;

            // Scale Hue to be between 0 and 360. Saturation
            // and value scale to be between 0 and 1.
            h = (double)color.Hue % 360;
            s = (double)color.Saturation / 100;
            v = (double)color.Value / 100;

            if (s == 0)
            {
                // If s is 0, all colors are the same.
                // This is some flavor of gray.
                r = v;
                g = v;
                b = v;
            }
            else
            {
                double p;
                double q;
                double t;

                double fractionalSector;
                int sectorNumber;
                double sectorPos;

                // The color wheel consists of 6 sectors.
                // Figure out which sector you're in.
                sectorPos = h / 60;
                sectorNumber = (int)(Math.Floor(sectorPos));

                // get the fractional part of the sector.
                // That is, how many degrees into the sector
                // are you?
                fractionalSector = sectorPos - sectorNumber;

                // Calculate values for the three axes
                // of the color. 
                p = v * (1 - s);
                q = v * (1 - (s * fractionalSector));
                t = v * (1 - (s * (1 - fractionalSector)));

                // Assign the fractional colors to r, g, and b
                // based on the sector the angle is in.
                switch (sectorNumber)
                {
                    case 0:
                        r = v;
                        g = t;
                        b = p;
                        break;

                    case 1:
                        r = q;
                        g = v;
                        b = p;
                        break;

                    case 2:
                        r = p;
                        g = v;
                        b = t;
                        break;

                    case 3:
                        r = p;
                        g = q;
                        b = v;
                        break;

                    case 4:
                        r = t;
                        g = p;
                        b = v;
                        break;

                    case 5:
                        r = v;
                        g = p;
                        b = q;
                        break;
                }
            }

            // return an RgbColor structure, with values scaled
            // to be between 0 and 255.
            return ColorBgra.FromBgr((byte)(b * 255), (byte)(g * 255), (byte)(r * 255));
        }
    }
}