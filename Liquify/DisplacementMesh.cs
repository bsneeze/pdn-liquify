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

        public unsafe void Render(ISurface<ColorBgra> dst, ISurface<ColorBgra> src, Rectangle rect, ColorBgra maskcolor)
        {
            UserBlendOp blendop = new UserBlendOps.NormalBlendOp();

            if (rect.Width == 0) return;

            ForEachRow(rect, y =>
            {
                DisplacementVector* offset = this.GetPointAddressUnchecked(rect.Left, y);
                ColorBgra* dstPixel = (ColorBgra*)dst.GetPointPointer(rect.Left, y);

                for (int x = rect.Left; x < rect.Right; ++x)
                {
                    ColorBgra mc = maskcolor.NewAlpha((byte)(maskcolor.A * offset->Mask / 510));

                    *dstPixel = blendop.Apply(src.GetBilinearSample(x + offset->X, y + offset->Y), mc);
                    ++offset;
                    ++dstPixel;
                }
            });
        }

        /// <summary>
        /// Draws the mesh grid over one row of an image that is being displayed zoomed. The grid is laid
        /// out on the source image, so it is distorted along with it, but it is evaluated per displayed
        /// pixel, so the lines stay thin at any zoom.
        /// </summary>
        /// <param name="pixels">the row's pixels: 32-bit BGRA, opaque</param>
        /// <param name="x">x of the first pixel, in displayed (zoomed) pixels</param>
        /// <param name="y">y of the row, in displayed pixels</param>
        /// <param name="count">number of pixels in the row</param>
        /// <param name="scale">displayed pixels per mesh pixel</param>
        /// <param name="spacing">distance between grid lines, in source pixels</param>
        /// <param name="halfLine">half the width of a grid line, in source pixels</param>
        public unsafe void DrawGridRow(IntPtr pixels, int x, int y, int count, float scale, float spacing, float halfLine)
        {
            uint* pixel = (uint*)pixels;
            float invScale = 1 / scale;

            // the mesh position under the middle of each displayed pixel
            float meshy = (y + 0.5f) * invScale - 0.5f;

            for (int i = 0; i < count; ++i, ++pixel)
            {
                float meshx = (x + i + 0.5f) * invScale - 0.5f;
                DisplacementVector offset = GetBilinearSample(meshx, meshy);
                float srcx = meshx + offset.X;
                float srcy = meshy + offset.Y;

                float fx = srcx - MathF.Floor(srcx / spacing) * spacing;
                float fy = srcy - MathF.Floor(srcy / spacing) * spacing;
                float distance = Math.Min(Math.Min(fx, spacing - fx), Math.Min(fy, spacing - fy));

                if (distance <= halfLine)
                {
                    // lighten dark pixels and darken light ones, so the line shows on anything
                    uint c = *pixel;
                    uint b = c & 255, g = (c >> 8) & 255, r = (c >> 16) & 255;
                    uint target = (r * 2 + g * 5 + b) / 8 < 128 ? 255u : 0u;

                    *pixel = 0xFF000000 | (((r + target) / 2) << 16) | (((g + target) / 2) << 8) | ((b + target) / 2);
                }
            }
        }

        const int maxSupersample = 4;

        /// <summary>
        /// Like Render, but where the mesh squeezes the source together it averages several samples per
        /// pixel instead of taking one, which would skip source pixels and look jagged. Everywhere else
        /// the result is the same as Render.
        /// </summary>
        public unsafe void RenderSupersampled(ISurface<ColorBgra> dst, ISurface<ColorBgra> src, Rectangle rect)
        {
            if (rect.Width == 0) return;

            ForEachRow(rect, y =>
            {
                DisplacementVector* offset = this.GetPointAddressUnchecked(rect.Left, y);
                ColorBgra* dstPixel = (ColorBgra*)dst.GetPointPointer(rect.Left, y);
                int down = y < height - 1 ? width : 0;

                for (int x = rect.Left; x < rect.Right; ++x)
                {
                    DisplacementVector* right = offset + (x < width - 1 ? 1 : 0);
                    DisplacementVector* below = offset + down;

                    // how far apart in the source the neighboring output pixels land
                    float ax = 1 + right->X - offset->X;
                    float ay = right->Y - offset->Y;
                    float bx = below->X - offset->X;
                    float by = 1 + below->Y - offset->Y;
                    float stretch = MathF.Sqrt(Math.Max(ax * ax + ay * ay, bx * bx + by * by));

                    if (stretch <= 1.05f)
                    {
                        *dstPixel = src.GetBilinearSample(x + offset->X, y + offset->Y);
                    }
                    else
                    {
                        int n = Math.Min(maxSupersample, Math.Max(2, (int)MathF.Ceiling(stretch)));
                        int a = 0, r = 0, g = 0, b = 0;

                        for (int j = 0; j < n; ++j)
                        {
                            float suby = y + (j + 0.5f) / n - 0.5f;

                            for (int i = 0; i < n; ++i)
                            {
                                float subx = x + (i + 0.5f) / n - 0.5f;
                                DisplacementVector v = GetBilinearSample(subx, suby);
                                ColorBgra s = src.GetBilinearSample(subx + v.X, suby + v.Y);

                                // weight by alpha so transparent samples don't darken the result
                                a += s.A;
                                r += s.R * s.A;
                                g += s.G * s.A;
                                b += s.B * s.A;
                            }
                        }

                        if (a == 0)
                        {
                            *dstPixel = ColorBgra.FromBgra(0, 0, 0, 0);
                        }
                        else
                        {
                            *dstPixel = ColorBgra.FromBgra(
                                (byte)((b + a / 2) / a),
                                (byte)((g + a / 2) / a),
                                (byte)((r + a / 2) / a),
                                (byte)((a + n * n / 2) / (n * n)));
                        }
                    }

                    ++offset;
                    ++dstPixel;
                }
            });
        }

        /// <summary>
        /// Removes all distortion. The mask is left alone.
        /// </summary>
        public unsafe void ClearOffsets()
        {
            ForEachRow(Bounds, y =>
            {
                DisplacementVector* ptr = GetPointAddressUnchecked(0, y);
                for (int x = 0; x < width; ++x)
                {
                    ptr->X = 0;
                    ptr->Y = 0;
                    ++ptr;
                }
            });
        }

        public unsafe void ClearMask()
        {
            ForEachRow(Bounds, y =>
            {
                DisplacementVector* ptr = GetPointAddressUnchecked(0, y);
                for (int x = 0; x < width; ++x)
                {
                    ptr->Mask = 0;
                    ++ptr;
                }
            });
        }

        public unsafe void InvertMask()
        {
            ForEachRow(Bounds, y =>
            {
                DisplacementVector* ptr = GetPointAddressUnchecked(0, y);
                for (int x = 0; x < width; ++x)
                {
                    ptr->Mask = (byte)(255 - ptr->Mask);
                    ++ptr;
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