using PaintDotNet.Effects;

namespace pyrochild.effects.liquify
{
    class ConfigToken : EffectConfigToken
    {
        public DisplacementMesh mesh;
        
        public int size;
        public float pressure;
        public float density;

        public ConfigToken()
        {
            size = 50;
            pressure = 0.25f;
            density = 0.25f;
        }

        public ConfigToken(ConfigToken toCopy)
        {
            this.mesh = toCopy.mesh;
            this.size = toCopy.size;
            this.pressure = toCopy.pressure;
            this.density = toCopy.density;
        }

        public override object Clone()
        {
            return new ConfigToken(this);
        }
    }
}