using System.Drawing;

namespace pyrochild.effects.liquify
{
    public struct HistoryItem
    {
        public Rectangle DeltaRect;
        public DiskBackedSurface DeltaSurface;

        public HistoryItem(DisplacementMesh mesh, Rectangle bounds)
        {
            DeltaRect = Rectangle.Intersect(mesh.Bounds, bounds);
            DisplacementMesh temp;
            if (DeltaRect.Width * DeltaRect.Height > 0)
            {
                temp = new DisplacementMesh(DeltaRect.Size);
                temp.Copy(mesh, temp.Bounds.Location, DeltaRect);
            }
            else
            {
                temp = new DisplacementMesh(1, 1);
                DeltaRect.Width = 1;
                DeltaRect.Height = 1;
            }
            DeltaSurface = new DiskBackedSurface(temp, true);
            DeltaSurface.ToDisk();
            temp.Dispose();
        }

        public HistoryItem(DisplacementMesh mesh)
        {
            DeltaRect = mesh.Bounds;
            DeltaSurface = new DiskBackedSurface(mesh, false);
            DeltaSurface.ToDisk();
        }
    }
}
