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

            SizeInt32 docSize = Environment.Document.Size;
            Size canvasSize = new Size(docSize.Width, docSize.Height);

            if (mesh.Size != canvasSize)
            {
                mesh = mesh.Resize(canvasSize);
            }

            RectInt32 bounds = output.Bounds;
            Rectangle canvasRect = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);

            // The mesh can pull a pixel from anywhere in the layer, so all of it is locked. Both it and
            // the output are used in place: ColorBgra32 and ColorBgra are the same four bytes.
            using (IEffectInputBitmap<ColorBgra32> srcBitmap = Environment.GetSourceBitmapBgra32())
            using (IBitmapLock<ColorBgra32> srcLock = srcBitmap.Lock(new RectInt32(0, 0, docSize.Width, docSize.Height)))
            using (IBitmapLock<ColorBgra32> dstLock = output.LockBgra32())
            {
                BitmapSurface source = new BitmapSurface((ColorBgra*)srcLock.Buffer, docSize.Width, docSize.Height, srcLock.BufferStride);
                BitmapSurface destination = BitmapSurface.ForRect((ColorBgra*)dstLock.Buffer, canvasRect, dstLock.BufferStride, canvasSize);

                mesh.RenderSupersampled(destination, source, canvasRect);
            }
        }
    }
}
