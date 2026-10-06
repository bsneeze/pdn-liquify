using PaintDotNet;
using PaintDotNet.Effects;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.Collections.Generic;

namespace pyrochild.effects.liquify
{
    /// <summary>
    /// Builds pictures of the document's other layers, for showing behind the one being edited.
    /// </summary>
    internal static class LayerCompositor
    {
        /// <summary>
        /// Blends a layer onto what has been built up so far, the way Paint.NET would for a layer
        /// with that blend mode and opacity.
        /// </summary>
        /// <param name="opacity">0 to 1</param>
        public static unsafe void BlendLayer(Surface destination, ISurface<ColorBgra> layer, LayerBlendMode blendMode, float opacity)
        {
            byte opacityByte = (byte)Math.Round(255 * Math.Clamp(opacity, 0f, 1f));
            CompositionOp op = LayerBlendModeUtil.CreateCompositionOp(blendMode, opacityByte);

            for (int y = 0; y < destination.Height; ++y)
            {
                op.Apply((ColorBgra*)destination.GetPointPointer(0, y), RowPointer(layer, y), destination.Width);
            }
        }

        private static unsafe ColorBgra* RowPointer(ISurface<ColorBgra> surface, int y)
        {
            return (ColorBgra*)((byte*)surface.Scan0 + (long)y * surface.Stride);
        }

        /// <summary>
        /// Passes a layer's pixels to use without copying them. They are only valid until it returns.
        /// </summary>
        private static unsafe void ReadLayer(IEffectLayerInfo layer, SizeInt32 size, Action<ISurface<ColorBgra>> use)
        {
            using (IEffectInputBitmap<ColorBgra32> bitmap = layer.GetBitmapBgra32())
            using (IBitmapLock<ColorBgra32> bitmapLock = bitmap.Lock(new RectInt32(0, 0, size.Width, size.Height)))
            {
                // ColorBgra32 and ColorBgra are the same four bytes
                use(new BitmapSurface((ColorBgra*)bitmapLock.Buffer, size.Width, size.Height, bitmapLock.BufferStride));
            }
        }

        /// <summary>
        /// Layers[first] up to but not including Layers[end], bottom first, composited onto a
        /// transparent surface the size of the document.
        /// </summary>
        /// <param name="visibleOnly">skip hidden layers, as the document itself does</param>
        public static Surface Render(IEffectDocumentInfo document, int first, int end, bool visibleOnly)
        {
            SizeInt32 size = document.Size;
            IReadOnlyList<IEffectLayerInfo> layers = document.Layers;

            Surface result = new Surface(size.Width, size.Height);
            result.Fill(ColorBgra.FromBgra(0, 0, 0, 0));

            for (int i = first; i < end; ++i)
            {
                IEffectLayerInfo layer = layers[i];
                if (visibleOnly && !layer.Visible)
                {
                    continue;
                }

                ReadLayer(layer, size, pixels => BlendLayer(result, pixels, layer.BlendMode, layer.Opacity));
            }

            return result;
        }

        /// <summary>
        /// The visible layers from Layers[first] up to but not including Layers[end], bottom first, as
        /// pictures for the canvas to draw over the image.
        /// </summary>
        public static List<pyrochild.effects.common.CanvasForegroundLayer> RenderForeground(IEffectDocumentInfo document, int first, int end)
        {
            SizeInt32 size = document.Size;
            IReadOnlyList<IEffectLayerInfo> layers = document.Layers;
            List<pyrochild.effects.common.CanvasForegroundLayer> result = new List<pyrochild.effects.common.CanvasForegroundLayer>();

            for (int i = first; i < end; ++i)
            {
                IEffectLayerInfo layer = layers[i];
                if (!layer.Visible)
                {
                    continue;
                }

                ReadLayer(layer, size, pixels => AppendForeground(result, pixels, layer.BlendMode, layer.Opacity));
            }

            return result;
        }

        /// <summary>
        /// Adds one layer to a foreground being built up from the bottom. A layer with a blend mode
        /// has to be blended onto whatever is under it when the canvas paints, so it gets a picture
        /// of its own. Runs of normal layers don't: laid over each other first and over the image
        /// afterwards, they come out the same, so each run shares one picture.
        /// </summary>
        public static unsafe void AppendForeground(List<pyrochild.effects.common.CanvasForegroundLayer> foreground, ISurface<ColorBgra> layer, LayerBlendMode blendMode, float opacity)
        {
            if (blendMode != LayerBlendMode.Normal)
            {
                // a copy: the caller's pixels may not outlive this call
                Surface copy = new Surface(layer.Width, layer.Height);
                long rowBytes = (long)layer.Width * sizeof(ColorBgra);
                for (int y = 0; y < layer.Height; ++y)
                {
                    Buffer.MemoryCopy(RowPointer(layer, y), (void*)copy.GetPointPointer(0, y), rowBytes, rowBytes);
                }

                foreground.Add(new pyrochild.effects.common.CanvasForegroundLayer(copy, CreateOp(blendMode, opacity)));
                return;
            }

            if (foreground.Count == 0 || foreground[foreground.Count - 1].Op != null)
            {
                Surface run = new Surface(layer.Width, layer.Height);
                run.Fill(ColorBgra.FromBgra(0, 0, 0, 0));
                foreground.Add(new pyrochild.effects.common.CanvasForegroundLayer(run));
            }

            BlendLayer(foreground[foreground.Count - 1].Surface, layer, LayerBlendMode.Normal, opacity);
        }

        /// <summary>
        /// The operation that blends a layer with this blend mode and opacity onto what is under it,
        /// or null when that is a plain overlay (normal, fully opaque).
        /// </summary>
        public static CompositionOp CreateOp(LayerBlendMode blendMode, float opacity)
        {
            byte opacityByte = (byte)Math.Round(255 * Math.Clamp(opacity, 0f, 1f));

            if (blendMode == LayerBlendMode.Normal && opacityByte == 255)
            {
                return null;
            }

            return LayerBlendModeUtil.CreateCompositionOp(blendMode, opacityByte);
        }

        public const int PreviewSize = 16;

        /// <summary>
        /// The same layers as Render, as a 16x16 picture for a menu entry. It reads one row of each
        /// layer per row of the picture, so it is quick however large the document is, at the cost
        /// of being a sample of the layers, not an average.
        /// </summary>
        public static unsafe System.Drawing.Bitmap RenderPreview(IEffectDocumentInfo document, int first, int end, bool visibleOnly)
        {
            SizeInt32 size = document.Size;
            IReadOnlyList<IEffectLayerInfo> layers = document.Layers;
            ColorBgra[] pixels = new ColorBgra[PreviewSize * PreviewSize]; // starts transparent

            for (int i = first; i < end; ++i)
            {
                IEffectLayerInfo layer = layers[i];
                if (visibleOnly && !layer.Visible)
                {
                    continue;
                }

                using (IEffectInputBitmap<ColorBgra32> bitmap = layer.GetBitmapBgra32())
                {
                    for (int row = 0; row < PreviewSize; ++row)
                    {
                        int y = SampleCenter(row, size.Height);

                        using (IBitmapLock<ColorBgra32> bitmapLock = bitmap.Lock(new RectInt32(0, y, size.Width, 1)))
                        {
                            ColorBgra* source = (ColorBgra*)bitmapLock.Buffer;

                            for (int column = 0; column < PreviewSize; ++column)
                            {
                                int index = row * PreviewSize + column;
                                pixels[index] = BlendPixel(pixels[index], source[SampleCenter(column, size.Width)], layer.BlendMode, layer.Opacity);
                            }
                        }
                    }
                }
            }

            System.Drawing.Bitmap preview = new System.Drawing.Bitmap(PreviewSize, PreviewSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            for (int row = 0; row < PreviewSize; ++row)
            {
                for (int column = 0; column < PreviewSize; ++column)
                {
                    ColorBgra c = pixels[row * PreviewSize + column];
                    preview.SetPixel(column, row, System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B));
                }
            }
            return preview;
        }

        /// <summary>
        /// The position along a length that the middle of preview cell number cell falls on.
        /// </summary>
        public static int SampleCenter(int cell, int length)
        {
            return (int)((2L * cell + 1) * length / (2 * PreviewSize));
        }

        /// <summary>
        /// BlendLayer for a single pixel.
        /// </summary>
        public static ColorBgra BlendPixel(ColorBgra under, ColorBgra layerPixel, LayerBlendMode blendMode, float opacity)
        {
            byte opacityByte = (byte)Math.Round(255 * Math.Clamp(opacity, 0f, 1f));
            return LayerBlendModeUtil.CreateCompositionOp(blendMode, opacityByte).Apply(under, layerPixel);
        }

        /// <summary>
        /// A 16x16 picture of a surface for a menu entry, the same size and made the same way as the
        /// canvas's own swatches. Transparent areas stay transparent.
        /// </summary>
        public static System.Drawing.Bitmap CreatePreview(Surface surface)
        {
            using (Surface small = new Surface(16, 16))
            {
                small.FitSurface(ResamplingAlgorithm.SuperSampling, surface);

                // a copy, because the aliased bitmap shares the surface's memory
                using (System.Drawing.Bitmap aliased = small.CreateAliasedBitmap())
                {
                    return new System.Drawing.Bitmap(aliased);
                }
            }
        }
    }
}
