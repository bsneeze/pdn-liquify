using PaintDotNet;
using PaintDotNet.Rendering;
using pyrochild.effects.common;
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace pyrochild.effects.liquify
{
    /// <summary>
    /// A 2D array of DisplacementVectors of the same size as the image.
    /// For any given point, the destination pixel is determined by adding the coordinate to
    /// the dispacement offset and using that as the new coordinate from which to pull the source pixel
    /// Dest image is essentially rendered by:
    /// DisplacementVector dv = this[x,y];
    /// dst[x,y] = src[x+dv.X, y+dv.Y]
    /// but with interpolation to handle non-integer source coordinates
    /// </summary>
    [Serializable]
    public class DisplacementMesh : ISurface
    {
        int stride;
        long bytes;
        MemoryBlock scan0;
        int width, height;
        int xstep, ystep;

        public DisplacementMesh(Size size)
            : this(size.Width, size.Height)
        { }

        public DisplacementMesh(int width, int height)
        {
            this.width = width;
            this.height = height;

            Allocate(width, height);
        }

        private void Allocate(int width, int height)
        {
            if ((width <= 0) || (height <= 0))
            {
                throw new ArgumentOutOfRangeException(string.Format("Width and Height must both be greater than zero. width={0}, height={1}", width, height));
            }
            try
            {
                stride = checked(width * System.Runtime.InteropServices.Marshal.SizeOf(typeof(DisplacementVector)));
                bytes = (long)height * stride;
            }
            catch (OverflowException ex)
            {
                throw new OutOfMemoryException("Dimensions are too large - not enough memory, width=" + width.ToString() + ", height=" + height.ToString(), ex);
            }
            scan0 = new MemoryBlock(bytes);

            // a 1 pixel wide or tall mesh has no neighbor to interpolate with
            xstep = width > 1 ? 1 : 0;
            ystep = height > 1 ? width : 0;
        }

        public unsafe DisplacementVector this[int x, int y]
        {
            get
            {
                if (x < 0 || x >= width || y < 0 || y >= height)
                    throw new ArgumentOutOfRangeException("x or y out of range");
                return *GetPointAddressUnchecked(x, y);
            }
        }

        public IntPtr Scan0
        {
            get { return scan0.Pointer; }
        }

        public int Stride
        {
            get { return stride; }
        }

        public int Width
        {
            get { return width; }
        }

        public int Height
        {
            get { return height; }
        }

        public Rectangle Bounds
        {
            get { return new Rectangle(0, 0, width, height); }
        }

        public Size Size
        {
            get { return new Size(width, height); }
        }

        public unsafe DisplacementVector* GetPointAddressUnchecked(int x, int y)
        {
            return unchecked(x + (DisplacementVector*)(((byte*)scan0.VoidStar) + ((long)y * stride)));
        }

        public unsafe void Render(ISurface<ColorBgra> dst, ISurface<ColorBgra> src, Rectangle rect)
        {
            if (rect.Width == 0) return;

            ForEachRow(rect, y =>
            {
                DisplacementVector* offset = this.GetPointAddressUnchecked(rect.Left, y);
                ColorBgra* dstPixel = (ColorBgra*)dst.GetPointPointer(rect.Left, y);

                for (int x = rect.Left; x < rect.Right; ++x)
                {
                    *dstPixel = src.GetBilinearSample(x + offset->X, y + offset->Y);
                    ++offset;
                    ++dstPixel;
                }
            });
        }

        const int parallelMinPixels = 128 * 128;

        /// <summary>
        /// Runs an action for every row of rect, in parallel once the rect is big enough to be worth it.
        /// The action must only write to its own row.
        /// </summary>
        internal static void ForEachRow(Rectangle rect, Action<int> row)
        {
            if ((long)rect.Width * rect.Height < parallelMinPixels)
            {
                for (int y = rect.Top; y < rect.Bottom; ++y)
                {
                    row(y);
                }
            }
            else
            {
                Parallel.For(rect.Top, rect.Bottom, row);
            }
        }

        public void Render(ISurface<ColorBgra> dst, ISurface<ColorBgra> src, Rectangle rect, ColorBgra maskcolor)
        {
            Render(dst, src, rect, maskcolor, 0, 0);
        }

        /// <summary>
        /// Renders with the mask tinted and, if gridSpacing is above zero, a grid drawn over the image.
        /// The grid is laid out on the source image, so it is distorted along with it.
        /// </summary>
        /// <param name="gridSpacing">distance between grid lines in source pixels, 0 for no grid</param>
        /// <param name="gridLineWidth">width of the grid lines in source pixels</param>
        public unsafe void Render(ISurface<ColorBgra> dst, ISurface<ColorBgra> src, Rectangle rect, ColorBgra maskcolor, int gridSpacing, float gridLineWidth)
        {
            UserBlendOp blendop = new UserBlendOps.NormalBlendOp();

            if (rect.Width == 0) return;

            float halfLine = gridLineWidth / 2;

            ForEachRow(rect, y =>
            {
                DisplacementVector* offset = this.GetPointAddressUnchecked(rect.Left, y);
                ColorBgra* dstPixel = (ColorBgra*)dst.GetPointPointer(rect.Left, y);

                for (int x = rect.Left; x < rect.Right; ++x)
                {
                    ColorBgra mc = maskcolor.NewAlpha((byte)(maskcolor.A * offset->Mask / 510));
                    float srcx = x + offset->X;
                    float srcy = y + offset->Y;

                    ColorBgra c = blendop.Apply(src.GetBilinearSample(srcx, srcy), mc);

                    if (gridSpacing > 0)
                    {
                        float fx = srcx - MathF.Floor(srcx / gridSpacing) * gridSpacing;
                        float fy = srcy - MathF.Floor(srcy / gridSpacing) * gridSpacing;
                        float distance = Math.Min(Math.Min(fx, gridSpacing - fx), Math.Min(fy, gridSpacing - fy));

                        if (distance <= halfLine)
                        {
                            // lighten dark pixels and darken light ones, so the line shows on anything
                            int target = (c.R * 2 + c.G * 5 + c.B) / 8 < 128 ? 255 : 0;
                            c = ColorBgra.FromBgra(
                                (byte)((c.B + target) / 2),
                                (byte)((c.G + target) / 2),
                                (byte)((c.R + target) / 2),
                                (byte)((c.A + 255) / 2));
                        }
                    }

                    *dstPixel = c;
                    ++offset;
                    ++dstPixel;
                }
            });
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                MemoryBlock scan0P = Interlocked.Exchange<MemoryBlock>(ref this.scan0, null);
                if (scan0P != null)
                {
                    scan0P.Dispose();
                    scan0P = null;
                }
            }
        }

        public bool IsDisposed
        {
            get { return (this.scan0 == null); }
        }

        public unsafe void Save(Stream s)
        {
            //              [NUL][NUL][NUL][STX]yfqLhseM[STX][NUL][NUL][NUL]
            byte[] header = { 0, 0, 0, 2, 0x79, 0x66, 0x71, 0x4c, 0x68, 0x73, 0x65, 0x4d, 2, 0, 0, 0 };
            using (BinaryWriter bw = new BinaryWriter(s))
            {
                bw.Write(header);
                bw.Write(width);
                bw.Write(height);

                long length = (long)width * height;
                DisplacementVector* ptr = (DisplacementVector*)Scan0;

                for (long i = 0; i < length; ++i)
                {
                    bw.Write(ptr->X);
                    bw.Write(ptr->Y);
                    ++ptr;
                }

                bw.Write((long)0);
            }
        }

        /// <summary>
        /// Writes the mesh memory as-is, Mask included. Unlike Save, this is not the .msh format
        /// and it leaves the stream open.
        /// </summary>
        internal void SaveRaw(Stream s)
        {
            SaveRaw(s, Bounds);
        }

        /// <summary>
        /// SaveRaw for just a part of the mesh. LoadRaw reads it back into a mesh the size of rect.
        /// </summary>
        internal unsafe void SaveRaw(Stream s, Rectangle rect)
        {
            int rowBytes = rect.Width * sizeof(DisplacementVector);

            for (int y = rect.Top; y < rect.Bottom; ++y)
            {
                s.Write(new ReadOnlySpan<byte>(GetPointAddressUnchecked(rect.Left, y), rowBytes));
            }
        }

        /// <summary>
        /// Reads back what SaveRaw wrote from a mesh of the same size.
        /// </summary>
        internal unsafe void LoadRaw(Stream s)
        {
            for (int y = 0; y < height; ++y)
            {
                s.ReadExactly(new Span<byte>(GetPointAddressUnchecked(0, y), stride));
            }
        }

        public void Load(Stream s)
        {
            using (BinaryReader br = new BinaryReader(s))
            {
                Size size = ReadHeader(br);

                if (size.Width == width && size.Height == height)
                {
                    LoadData(br);
                }
                else
                {
                    DisplacementMesh loaded = new DisplacementMesh(size);
                    loaded.LoadData(br);

                    DisplacementMesh resized = loaded.Resize(this.Size);
                    loaded.Dispose();

                    // only take the offsets, so Mask is left alone just like in a same-size load
                    CopyOffsets(resized);
                    resized.Dispose();
                }
            }
        }

        private unsafe void CopyOffsets(DisplacementMesh srcMesh)
        {
            for (int y = 0; y < height; ++y)
            {
                DisplacementVector*
                    src = srcMesh.GetPointAddressUnchecked(0, y),
                    dst = this.GetPointAddressUnchecked(0, y);
                for (int x = 0; x < width; ++x)
                {
                    dst->X = src->X;
                    dst->Y = src->Y;
                    ++src;
                    ++dst;
                }
            }
        }
        
        private static Size ReadHeader(BinaryReader br)
        {
            if (br.BaseStream.Length - br.BaseStream.Position < 24)
                throw new FileFormatException("Not a valid mesh file - file too small");

            br.ReadBytes(4);

            if (br.ReadBytes(8).ToString(Encoding.ASCII) != "yfqLhseM")
                throw new FileFormatException("Not a valid mesh file - invalid header");

            br.ReadBytes(4);

            int w = br.ReadInt32();
            int h = br.ReadInt32();

            return new Size(w, h);
        }

        private unsafe void LoadData(BinaryReader br)
        {
            long length = Math.Min((br.BaseStream.Length - br.BaseStream.Position) / 8 - 1, (long)width * height);

            DisplacementVector* ptr = (DisplacementVector*)Scan0;

            for (long i = 0; i < length; ++i)
            {
                ptr->X = br.ReadSingle();
                ptr->Y = br.ReadSingle();
                ++ptr;
            }
        }

        public unsafe DisplacementVector GetBilinearSample(float x, float y)
        {
            int x0, y0;

            if (x >= width - 1)
            {
                x = width - 1;
                x0 = Math.Max(width - 2, 0);
            }
            else
            {
                if (x < 0) x = 0;
                x0 = (int)x;
            }

            if (y >= height - 1)
            {
                y = height - 1;
                y0 = Math.Max(height - 2, 0);
            }
            else
            {
                if (y < 0) y = 0;
                y0 = (int)y;
            }

            float factorX = x - x0;

            DisplacementVector*
                tl = GetPointAddressUnchecked(x0, y0),
                bl = tl + ystep;

            DisplacementVector
                t = DisplacementVector.Lerp(*tl, *(tl + xstep), factorX),
                b = DisplacementVector.Lerp(*bl, *(bl + xstep), factorX);

            return DisplacementVector.Lerp(t, b, y - y0);
        }

        public unsafe DisplacementMesh Resize(Size size)
        {
            DisplacementMesh ret = new DisplacementMesh(size);

            float xfactor = (float)width / size.Width;
            float yfactor = (float)height / size.Height;
            for (int y = 0; y < size.Height; ++y)
            {
                DisplacementVector* ptr = ret.GetPointAddressUnchecked(0, y);
                float srcy = y * yfactor; ;
                for (int x = 0; x < size.Width; ++x)
                {
                    float srcx = x * xfactor;
                    DisplacementVector v = GetBilinearSample(srcx, srcy);
                    ptr->X = v.X / xfactor;
                    ptr->Y = v.Y / yfactor;
                    ++ptr;
                }
            }
            return ret;
        }

        public unsafe void Copy(DisplacementMesh srcMesh, Point dstOffset, Rectangle srcRect)
        {
            long rowBytes = (long)srcRect.Width * sizeof(DisplacementVector);

            for (int y = 0; y < srcRect.Height; ++y)
            {
                DisplacementVector*
                    src = srcMesh.GetPointAddressUnchecked(srcRect.X, y + srcRect.Y),
                    dst = this.GetPointAddressUnchecked(dstOffset.X, y + dstOffset.Y);
                Buffer.MemoryCopy(src, dst, rowBytes, rowBytes);
            }
        }

        public unsafe DisplacementMesh Clone()
        {
            DisplacementMesh ret = new DisplacementMesh(width, height);
            ret.Copy(this, Point.Empty, this.Bounds);
            return ret;
        }
    }
}