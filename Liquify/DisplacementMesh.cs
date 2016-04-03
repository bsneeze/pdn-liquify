using PaintDotNet;
using PaintDotNet.Rendering;
using pyrochild.effects.common;
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;

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
                stride = width * System.Runtime.InteropServices.Marshal.SizeOf(typeof(DisplacementVector));
                bytes = height * stride;
            }
            catch (OverflowException ex)
            {
                throw new OutOfMemoryException("Dimensions are too large - not enough memory, width=" + width.ToString() + ", height=" + height.ToString(), ex);
            }
            scan0 = new MemoryBlock(bytes);
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
            return unchecked(x + (DisplacementVector*)(((byte*)scan0.VoidStar) + (y * stride)));
        }

        public unsafe void Render(ISurface<ColorBgra> dst, ISurface<ColorBgra> src, Rectangle rect)
        {
            if (rect.Width == 0) return;

            for (int y = rect.Top; y < rect.Bottom; ++y)
            {
                DisplacementVector* offset = this.GetPointAddressUnchecked(rect.Left, y);
                ColorBgra* dstPixel = (ColorBgra*)dst.GetPointPointer(rect.Left, y);

                for (int x = rect.Left; x < rect.Right; ++x)
                {
                    *dstPixel = src.GetBilinearSample(x + offset->X, y + offset->Y);
                    ++offset;
                    ++dstPixel;
                }
            }
        }

        public unsafe void Render(ISurface<ColorBgra> dst, ISurface<ColorBgra> src, Rectangle rect, ColorBgra maskcolor)
        {
            UserBlendOp blendop = new UserBlendOps.NormalBlendOp();

            if (rect.Width == 0) return;

            for (int y = rect.Top; y < rect.Bottom; ++y)
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
            }
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

                    this.Dispose(true); // safe to toss our own data at this point

                    DisplacementMesh resized = loaded.Resize(this.Size);
                    loaded.Dispose();

                    this.scan0 = resized.scan0;
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
                x0 = width - 2;
            }
            else
            {
                if (x < 0) x = 0;
                x0 = (int)x;
            }

            if (y >= height - 1)
            {
                y = height - 1;
                y0 = height - 2;
            }
            else
            {
                if (y < 0) y = 0;
                y0 = (int)y;
            }

            float factorX = x - x0;

            DisplacementVector*
                tl = GetPointAddressUnchecked(x0, y0),
                bl = tl + width;

            DisplacementVector
                t = DisplacementVector.Lerp(*tl, *(tl + 1), factorX),
                b = DisplacementVector.Lerp(*bl, *(bl + 1), factorX);

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
            for (int y = 0; y < srcRect.Height; ++y)
            {
                DisplacementVector*
                    src = srcMesh.GetPointAddressUnchecked(srcRect.X, y + srcRect.Y),
                    dst = this.GetPointAddressUnchecked(dstOffset.X, y + dstOffset.Y);
                for (int x = 0; x < srcRect.Width; ++x)
                {
                    *dst = *src;
                    ++src;
                    ++dst;
                }
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