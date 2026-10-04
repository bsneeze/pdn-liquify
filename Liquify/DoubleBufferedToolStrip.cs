using System.Windows.Forms;

namespace pyrochild.effects.liquify
{
    // WS_EX_COMPOSITED stops hosted combo boxes from flickering on hover/select.
    internal class DoubleBufferedToolStrip : ToolStrip
    {
        private const int WS_EX_COMPOSITED = 0x02000000;

        public DoubleBufferedToolStrip()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_COMPOSITED;
                return cp;
            }
        }
    }
}
