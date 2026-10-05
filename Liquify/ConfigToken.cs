using PaintDotNet.Effects;

namespace pyrochild.effects.liquify
{
    public enum CanvasBackground
    {
        Color,
        LayerBeneath,
        AllLayersBeneath
    }

    public class ConfigToken : EffectConfigToken
    {
        public DisplacementMesh mesh;
        
        public int size;
        public float pressure;
        public float density;

        // What the dialog shows behind the layer's transparent parts. Like the brush settings, it
        // has no effect on the result; it is here so the dialog opens the way it was left.
        public CanvasBackground background;
        public int backgroundColor; // ARGB, used when background is Color
        public bool showLayersAbove;
        public int surroundColor; // ARGB of the area around the canvas; 0 for the default
        public int meshGrid;      // 0 off, 1 fine, 2 coarse

        public ConfigToken()
        {
            size = 50;
            pressure = 0.25f;
            density = 0.25f;
            background = CanvasBackground.Color;
            backgroundColor = 0; // transparent: the checkerboard
        }

        public ConfigToken(ConfigToken toCopy)
        {
            this.mesh = toCopy.mesh;
            this.size = toCopy.size;
            this.pressure = toCopy.pressure;
            this.density = toCopy.density;
            this.background = toCopy.background;
            this.backgroundColor = toCopy.backgroundColor;
            this.showLayersAbove = toCopy.showLayersAbove;
            this.surroundColor = toCopy.surroundColor;
            this.meshGrid = toCopy.meshGrid;
        }

        public override object Clone()
        {
            return new ConfigToken(this);
        }
    }
}