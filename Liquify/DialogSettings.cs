namespace pyrochild.effects.liquify
{
    public enum CanvasBackground
    {
        Color,
        LayerBeneath,
        AllLayersBeneath
    }

    /// <summary>
    /// How the dialog is set up: the brush and what the canvas shows. None of it affects the
    /// result, so it isn't part of the token. The dialog keeps the last one for as long as
    /// Paint.NET runs, so that it opens the way it was left.
    /// </summary>
    internal sealed class DialogSettings
    {
        public int size = 50;
        public float pressure = 0.25f;
        public float density = 0.25f;

        // what shows behind the layer's transparent parts
        public CanvasBackground background = CanvasBackground.Color;
        public int backgroundColor; // ARGB, used when background is Color; 0 is the checkerboard

        public bool showLayersAbove;
        public int surroundColor; // ARGB of the area around the canvas; 0 for the default
        public int meshGrid;      // 0 off, 1 fine, 2 coarse
    }
}
