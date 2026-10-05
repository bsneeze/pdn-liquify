using System.Drawing;

namespace pyrochild.effects.liquify
{
    /// <summary>
    /// One undoable change: the contents of DeltaRect before and after it.
    /// </summary>
    public struct HistoryItem
    {
        public Rectangle DeltaRect;
        public DiskBackedSurface Before;
        public DiskBackedSurface After;

        /// <param name="rect">must be non-empty and inside the bounds of both meshes</param>
        public HistoryItem(DisplacementMesh before, DisplacementMesh after, Rectangle rect)
        {
            DeltaRect = rect;
            Before = Capture(before, rect);
            After = Capture(after, rect);
        }

        private static DiskBackedSurface Capture(DisplacementMesh mesh, Rectangle rect)
        {
            DisplacementMesh temp = new DisplacementMesh(rect.Size);
            temp.Copy(mesh, Point.Empty, rect);

            DiskBackedSurface ret = new DiskBackedSurface(temp, true);
            ret.ToDisk();
            return ret;
        }

        public void Dispose()
        {
            Before.Dispose();
            After.Dispose();
        }
    }
}