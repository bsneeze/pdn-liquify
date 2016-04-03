using PaintDotNet;
using PaintDotNet.Effects;
using System.Drawing;

namespace pyrochild.effects.liquify
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    public sealed class Liquify : Effect
    {
        DisplacementMesh mesh;

        public Liquify() : base(StaticName, StaticIcon, StaticSubMenu, EffectFlags.Configurable) { }

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

        public override EffectConfigDialog CreateConfigDialog()
        {
            return new ConfigDialog();
        }

        protected override void OnSetRenderInfo(EffectConfigToken parameters, RenderArgs dstArgs, RenderArgs srcArgs)
        {
            base.OnSetRenderInfo(parameters, dstArgs, srcArgs);

            ConfigToken token = parameters as ConfigToken;
            if (token != null && token.mesh != null)
            {
                mesh = token.mesh;
                if (mesh.Size != dstArgs.Surface.Size)
                {
                    mesh = mesh.Resize(dstArgs.Size);
                }
            }
        }

        public override void Render(EffectConfigToken parameters, RenderArgs dstArgs, RenderArgs srcArgs, Rectangle[] rois, int startIndex, int length)
        {
            if (mesh != null)
            {
                for (int i = startIndex; i < startIndex + length; ++i)
                    mesh.Render(dstArgs.Surface, srcArgs.Surface, rois[i]);
            }
        }
    }
}