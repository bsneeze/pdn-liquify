using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using PaintDotNet;
using PaintDotNet.Imaging;
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

        public static ColorBgra ToColorBgra(this ColorHsv96Float color)
        {
            ColorRgb96Float rgb = color.ToRgb();
            return ColorBgra.FromBgraClamped(rgb.B * 255f, rgb.G * 255f, rgb.R * 255f, 255f);
        }

        public static ColorHsv96Float ToHsvColor(this ColorBgra color)
        {
            ColorRgb96Float rgb = new ColorRgb96Float(color.R / 255f, color.G / 255f, color.B / 255f);
            return rgb.ToHsv();
        }
    }
}