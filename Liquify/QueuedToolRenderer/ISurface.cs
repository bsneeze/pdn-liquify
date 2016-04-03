using System;
using System.Drawing;

namespace pyrochild.effects.common
{
    public interface ISurface : IDisposable
    {
        Size Size { get; }

        Rectangle Bounds { get; }
    }
}