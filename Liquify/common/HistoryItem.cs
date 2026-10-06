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

        /// <param name="before">what rect held before the change; the item takes it over, unless this throws</param>
        /// <param name="after">the mesh as it is now</param>
        /// <param name="rect">must be non-empty and inside the mesh</param>
        public HistoryItem(DiskBackedSurface before, DisplacementMesh after, Rectangle rect)
        {
            DeltaRect = rect;
            Before = before;

            After = DiskBackedSurface.FromRect(after, rect);
        }

        public void Dispose()
        {
            Before.Dispose();
            After.Dispose();
        }
    }
}
