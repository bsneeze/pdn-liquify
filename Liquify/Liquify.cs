using PaintDotNet;
using PaintDotNet.Effects;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System.Drawing;

namespace pyrochild.effects.liquify
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    public sealed class Liquify : BitmapEffect<ConfigToken>
    {
        DisplacementMesh mesh;
        Surface cachedSource;

        public Liquify() : base(StaticName, StaticIcon, StaticSubMenu, BitmapEffectOptions.Create() with { IsConfigurable = true }) { }

        internal static string RawName { get { return "Liquify"; } }
        public static string StaticName
        {
            get
            {
                string name = RawName;
#if DEBUG
                name += " BETA";
#endif
                return name;
            }
        }

        public static string StaticDialogName
        {
            get { return StaticName + " by pyrochild"; }
        }

        public static Bitmap StaticIcon = new Bitmap(typeof(Liquify), "images.icon.png");

        public static string StaticSubMenu
        {
            get
            {
                return "Tools";
            }
        }

        protected override IEffectConfigForm OnCreateConfigForm()
        {
            return new ConfigDialog();
        }

        protected override void OnInitializeRenderInfo(IBitmapEffectRenderInfo renderInfo)
        {
            base.OnInitializeRenderInfo(renderInfo);
            renderInfo.Schedule = BitmapEffectRenderingSchedule.None;
        }

        protected override void OnSetToken(ConfigToken newToken)
        {
            base.OnSetToken(newToken);

            if (newToken != null && newToken.mesh != null)
            {
                mesh = newToken.mesh;
            }
        }

        protected override unsafe void OnRender(IBitmapEffectOutput output)
        {
            if (mesh == null)
            {
                return;
            }

            if (cachedSource == null)
            {
                SizeInt32 docSize = Environment.Document.Size;
                Size canvasSize = new Size(docSize.Width, docSize.Height);

                if (mesh.Size != canvasSize)
                {
                    mesh = mesh.Resize(canvasSize);
                }

                cachedSource = new Surface(docSize.Width, docSize.Height);
                using (IEffectInputBitmap<ColorBgra32> srcBitmap = Environment.GetSourceBitmapBgra32())
                using (IBitmapLock<ColorBgra32> srcLock = srcBitmap.Lock(new RectInt32(0, 0, docSize.Width, docSize.Height)))
                {
                    RegionPtr<ColorBgra32> srcRegion32 = new RegionPtr<ColorBgra32>(srcLock.Buffer, srcLock.Size, srcLock.BufferStride);
                    RegionPtr<ColorBgra> dstRegion = new RegionPtr<ColorBgra>(cachedSource.GetPointPointer(0, 0), cachedSource.Width, cachedSource.Height, cachedSource.Stride);
                    srcRegion32.Cast<ColorBgra>().CopyTo(dstRegion);
                }
            }

            RectInt32 bounds = output.Bounds;
            Rectangle canvasRect = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);

            using (Surface tempDst = new Surface(cachedSource.Size))
            {
                mesh.Render(tempDst, cachedSource, canvasRect);

                using (IBitmapLock<ColorBgra32> dstLock = output.LockBgra32())
                {
                    RegionPtr<ColorBgra32> dstRegion = new RegionPtr<ColorBgra32>(dstLock.Buffer, dstLock.Size, dstLock.BufferStride);
                    RegionPtr<ColorBgra> srcRegion = new RegionPtr<ColorBgra>(tempDst.GetPointPointer(bounds.X, bounds.Y), bounds.Width, bounds.Height, tempDst.Stride);
                    srcRegion.Cast<ColorBgra32>().CopyTo(dstRegion);
                }
            }
        }

        protected override void OnDispose(bool disposing)
        {
            if (disposing)
            {
                cachedSource?.Dispose();
                cachedSource = null;
            }
            base.OnDispose(disposing);
        }
    }
}
