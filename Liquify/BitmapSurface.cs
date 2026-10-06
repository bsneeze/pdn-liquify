using PaintDotNet;
using PaintDotNet.Rendering;

namespace pyrochild.effects.liquify
{
    /// <summary>
    /// Lets pixels that are already in memory, such as a locked Paint.NET 5 bitmap, be read as a
    /// surface without copying them. It doesn't own the memory: whoever does has to keep it alive
    /// for as long as this is in use.
    /// </summary>
    internal sealed unsafe class BitmapSurface : ISurface<ColorBgra>
    {
        private readonly ColorBgra* scan0;
        private readonly int width;
        private readonly int height;
        private readonly int stride;

        /// <param name="stride">bytes from one row to the next</param>
        public BitmapSurface(ColorBgra* scan0, int width, int height, int stride)
        {
            this.scan0 = scan0;
            this.width = width;
            this.height = height;
            this.stride = stride;
        }

        /// <summary>
        /// For memory that holds only one rectangle of a larger picture, such as an effect's output.
        /// The surface is the size of the whole picture, so coordinates stay the picture's own, but
        /// only the pixels inside rect exist: nothing outside it may be read or written.
        /// </summary>
        /// <param name="rectPixels">the pixel at rect's top left</param>
        public static BitmapSurface ForRect(ColorBgra* rectPixels, System.Drawing.Rectangle rect, int stride, System.Drawing.Size pictureSize)
        {
            // where the picture's (0, 0) would be if the rest of it were there
            ColorBgra* origin = (ColorBgra*)((byte*)rectPixels - (long)rect.Y * stride) - rect.X;
            return new BitmapSurface(origin, pictureSize.Width, pictureSize.Height, stride);
        }

        public ColorBgra* Scan0 { get { return scan0; } }
        public int Stride { get { return stride; } }
        public int Width { get { return width; } }
        public int Height { get { return height; } }
        public bool IsDisposed { get { return false; } }

        public void Dispose()
        {
            // nothing to free: the memory is someone else's
        }

        public void Render(RegionPtr<ColorBgra> dst, Point2Int32 offset)
        {
            RegionPtr<ColorBgra> all = new RegionPtr<ColorBgra>(scan0, width, height, stride);
            all.Slice(new RectInt32(offset.X, offset.Y, dst.Width, dst.Height)).CopyTo(dst);
        }
    }
}
