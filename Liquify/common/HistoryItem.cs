using System.Drawing;

namespace pyrochild.effects.liquify
{
    /// <summary>
    /// One undoable change: what the mesh held before and after it. A brush stroke keeps only
    /// the tiles it touched, which for a long thin stroke is a small part of the rectangle
    /// around it. A whole-mesh change keeps that rectangle in one piece.
    /// </summary>
    public struct HistoryItem
    {
        /// <summary>An area that contains everything the change touched.</summary>
        public Rectangle DeltaRect;

        /// <summary>
        /// Where on the mesh each stored tile goes. Before and After then hold them as
        /// DiskBackedSurface.FromPieces lays them out. Null when they hold DeltaRect itself.
        /// </summary>
        public Rectangle[] Tiles;

        public DiskBackedSurface Before;
        public DiskBackedSurface After;

        /// <param name="before">what rect held before the change; the item takes it over, unless this throws</param>
        /// <param name="after">the mesh as it is now</param>
        /// <param name="rect">must be non-empty and inside the mesh</param>
        public HistoryItem(DiskBackedSurface before, DisplacementMesh after, Rectangle rect)
        {
            DeltaRect = rect;
            Tiles = null;
            Before = before;

            After = DiskBackedSurface.FromRect(after, rect);
        }

        /// <param name="tiles">where each tile is on the mesh, in the order before and after hold them</param>
        public HistoryItem(Rectangle rect, Rectangle[] tiles, DiskBackedSurface before, DiskBackedSurface after)
        {
            DeltaRect = rect;
            Tiles = tiles;
            Before = before;
            After = after;
        }

        public void Dispose()
        {
            Before.Dispose();
            After.Dispose();
        }
    }
}
