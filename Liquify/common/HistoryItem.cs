using System.Drawing;
using System.Threading.Tasks;

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

            // the two halves are independent, so write them side by side
            Task<DiskBackedSurface> beforeTask = Task.Run(() => DiskBackedSurface.FromRect(before, rect));
            try
            {
                After = DiskBackedSurface.FromRect(after, rect);
            }
            catch
            {
                try { beforeTask.Result.Dispose(); } catch { }
                throw;
            }

            try
            {
                Before = beforeTask.Result;
            }
            catch
            {
                After.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Before.Dispose();
            After.Dispose();
        }
    }
}
