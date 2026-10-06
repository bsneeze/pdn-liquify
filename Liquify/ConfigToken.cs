using PaintDotNet.Effects;

namespace pyrochild.effects.liquify
{
    public class ConfigToken : EffectConfigToken
    {
        public DisplacementMesh mesh;

        public ConfigToken()
        {
        }

        public ConfigToken(ConfigToken toCopy)
        {
            this.mesh = toCopy.mesh;
        }

        public override object Clone()
        {
            return new ConfigToken(this);
        }
    }
}
