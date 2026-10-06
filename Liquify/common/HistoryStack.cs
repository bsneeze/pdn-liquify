using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace pyrochild.effects.liquify
{
    /// <summary>
    /// Undo and redo for a mesh. Each step holds the changed part of the mesh before and after it.
    /// The "before" half has to be captured before the mesh is written to, so whoever changes the
    /// mesh calls BeforeChange for each area first (as often as it likes), and AddHistoryItem once
    /// the change is complete. BeforeChange may come from another thread than the rest.
    /// Running out of memory or disk space never leaves the mesh changed without a way back: either
    /// BeforeChange says no and the mesh must be left alone there, or AddHistoryItem puts back what
    /// the change overwrote.
    /// </summary>
    /// <summary>What a history operation that returned false ran out of.</summary>
    public enum HistoryFailure
    {
        None,
        Memory,
        Disk
    }

    public class HistoryStack : IDisposable
    {
        /// <summary>
        /// Why BeforeChange or AddHistoryItem last returned false.
        /// </summary>
        public HistoryFailure LastFailure { get; private set; }

        // Everything that can fail here is either an allocation or a write to the temporary files.
        private static HistoryFailure Classify(Exception ex)
        {
            return ex is OutOfMemoryException ? HistoryFailure.Memory : HistoryFailure.Disk;
        }

        List<HistoryItem> stack;
        int step; // index of the last applied item, -1 if there is none
        bool errornotified;

        // What the change in progress has overwritten so far: a copy of each tile of the mesh, made
        // the first time BeforeChange is told about it. Only the tiles a stroke touches are held,
        // and only until the stroke ends, instead of a second copy of the whole mesh.
        private const int tileSize = 64;
        private readonly Dictionary<long, DisplacementMesh> beforeTiles = new Dictionary<long, DisplacementMesh>();

        // A change to the whole mesh is saved in one piece instead, straight to disk.
        private DiskBackedSurface beforeWhole;

        private readonly object sync = new object();

        // how the pieces of mesh held here are allocated; tests replace it to stand in for a machine
        // that has run out of memory
        internal Func<int, int, DisplacementMesh> NewMesh = (width, height) => new DisplacementMesh(width, height);

        public HistoryStack()
        {
            stack = new List<HistoryItem>();
            step = -1;
        }

        private void RemoveRedoItems()
        {
            if (step < stack.Count - 1)
            {
                for (int i = step + 1; i < stack.Count; i++)
                {
                    stack[i].Dispose();
                }
                stack.RemoveRange(step + 1, stack.Count - 1 - step);
            }
        }

        /// <summary>
        /// Must be called before the mesh is changed anywhere in bounds. Calling it again for an
        /// area it has already seen costs next to nothing.
        /// </summary>
        /// <returns>
        /// False if what is there could not be saved, for want of memory or disk space. The mesh
        /// must then be left as it is within bounds.
        /// </returns>
        public bool BeforeChange(DisplacementMesh mesh, Rectangle bounds)
        {
            Rectangle rect = Rectangle.Intersect(mesh.Bounds, bounds);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return true;
            }

            lock (sync)
            {
                if (beforeWhole != null)
                {
                    return true; // everything is saved already
                }

                try
                {
                    if (rect == mesh.Bounds && beforeTiles.Count == 0)
                    {
                        beforeWhole = DiskBackedSurface.FromRect(mesh, rect);
                        return true;
                    }

                    for (int ty = rect.Top / tileSize; ty <= (rect.Bottom - 1) / tileSize; ++ty)
                    {
                        for (int tx = rect.Left / tileSize; tx <= (rect.Right - 1) / tileSize; ++tx)
                        {
                            long key = TileKey(tx, ty);
                            if (!beforeTiles.ContainsKey(key))
                            {
                                Rectangle tile = TileRect(tx, ty, mesh);
                                DisplacementMesh copy = NewMesh(tile.Width, tile.Height);
                                copy.Copy(mesh, Point.Empty, tile);
                                beforeTiles.Add(key, copy);
                            }
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    // the tiles saved so far stay: they are still what the change has overwritten
                    LastFailure = Classify(ex);
                    return false;
                }
            }
        }

        private static long TileKey(int tx, int ty)
        {
            return ((long)ty << 32) | (uint)tx;
        }

        private static Rectangle TileRect(int tx, int ty, DisplacementMesh mesh)
        {
            return Rectangle.Intersect(new Rectangle(tx * tileSize, ty * tileSize, tileSize, tileSize), mesh.Bounds);
        }

        /// <summary>
        /// Forgets what BeforeChange has saved, for a change that was not made after all.
        /// </summary>
        public void CancelChange()
        {
            lock (sync)
            {
                ClearPending();
            }
        }

        /// <summary>
        /// Puts back what BeforeChange has saved and forgets it, for a change that failed part way.
        /// </summary>
        public void RevertChange(DisplacementMesh mesh)
        {
            lock (sync)
            {
                try
                {
                    Restore(mesh);
                }
                finally
                {
                    ClearPending();
                }
            }
        }

        private void ClearPending()
        {
            foreach (DisplacementMesh tile in beforeTiles.Values)
            {
                tile.Dispose();
            }
            beforeTiles.Clear();

            if (beforeWhole != null)
            {
                beforeWhole.Dispose();
                beforeWhole = null;
            }
        }

        /// <summary>
        /// How many mesh vectors are being held for the change in progress.
        /// </summary>
        internal long PendingVectorCount
        {
            get
            {
                lock (sync)
                {
                    long count = 0;
                    foreach (DisplacementMesh tile in beforeTiles.Values)
                    {
                        count += (long)tile.Width * tile.Height;
                    }
                    return count;
                }
            }
        }

        /// <summary>
        /// Records the change that BeforeChange was called for, now that it is complete.
        /// </summary>
        /// <param name="bounds">an area that contains everything that changed</param>
        /// <returns>
        /// False if the change could not be recorded, for want of memory or disk space. The mesh
        /// has then been put back the way it was before the change, and there is no new step.
        /// </returns>
        public bool AddHistoryItem(DisplacementMesh mesh, Rectangle bounds)
        {
            try
            {
                HistoryItem? item;
                if (CaptureChange(mesh, bounds, out item) != HistoryFailure.None)
                {
                    return false;
                }

                if (item.HasValue)
                {
                    Commit(item.Value);
                }
                return true;
            }
            catch (Exception ex)
            {
                // not even putting it back worked
                OnError(ex);
                return false;
            }
        }

        /// <summary>
        /// The slow half of AddHistoryItem: writes out the change that BeforeChange was called
        /// for. Like BeforeChange it may run on whichever thread owns the mesh, and it doesn't
        /// touch the steps themselves, so undo state can be read from the UI thread meanwhile.
        /// </summary>
        /// <param name="item">
        /// The step to hand to Commit, or to dispose if it isn't wanted after all. Null when
        /// nothing changed or the capture failed.
        /// </param>
        /// <returns>
        /// What ran out, if the change could not be captured. The mesh has then been put back the
        /// way it was before the change. Throws if even that failed.
        /// </returns>
        public HistoryFailure CaptureChange(DisplacementMesh mesh, Rectangle bounds, out HistoryItem? item)
        {
            item = null;

            lock (sync)
            {
                try
                {
                    Rectangle rect = Rectangle.Intersect(mesh.Bounds, bounds);
                    if (rect.Width <= 0 || rect.Height <= 0)
                    {
                        return HistoryFailure.None;
                    }

                    if (beforeWhole == null && beforeTiles.Count == 0)
                    {
                        return HistoryFailure.None; // nothing was saved, so nothing can have changed
                    }

                    DiskBackedSurface before = null;
                    try
                    {
                        if (beforeWhole == null)
                        {
                            item = CaptureTiles(mesh, rect);
                        }
                        else
                        {
                            before = TakeBefore(mesh, rect);
                            item = new HistoryItem(before, mesh, rect);
                        }
                    }
                    catch (Exception ex)
                    {
                        HistoryFailure failure = Classify(ex);
                        LastFailure = failure;
                        if (before != null && before != beforeWhole)
                        {
                            before.Dispose();
                        }
                        Restore(mesh);
                        return failure;
                    }

                    if (before == beforeWhole)
                    {
                        beforeWhole = null; // the item owns it now
                    }

                    return HistoryFailure.None;
                }
                finally
                {
                    ClearPending();
                }
            }
        }

        /// <summary>
        /// Makes a captured change the newest step. Like undo and redo, only from the UI thread.
        /// </summary>
        public void Commit(HistoryItem item)
        {
            RemoveRedoItems();
            stack.Add(item);
            step++;
        }

        // Puts back what the change in progress has overwritten. Copying the tiles back needs no
        // memory; a whole-mesh change has to be read back from disk.
        private void Restore(DisplacementMesh mesh)
        {
            if (beforeWhole != null)
            {
                beforeWhole.ToMemory();
                mesh.Copy(beforeWhole.Surface, Point.Empty, mesh.Bounds);
            }

            foreach (KeyValuePair<long, DisplacementMesh> saved in beforeTiles)
            {
                Rectangle tile = TileRect((int)(uint)saved.Key, (int)(saved.Key >> 32), mesh);
                mesh.Copy(saved.Value, tile.Location, new Rectangle(0, 0, tile.Width, tile.Height));
            }
        }

        // A step made of the tiles that were saved for the change in progress: each as it was
        // saved, and as it is in the mesh now. Both go straight to disk, so this needs no memory
        // to speak of however large the change is.
        private HistoryItem CaptureTiles(DisplacementMesh mesh, Rectangle rect)
        {
            long[] keys = new long[beforeTiles.Count];
            beforeTiles.Keys.CopyTo(keys, 0);
            Array.Sort(keys);

            Rectangle[] tiles = new Rectangle[keys.Length];
            Rectangle[] savedRects = new Rectangle[keys.Length];
            DisplacementMesh[] saved = new DisplacementMesh[keys.Length];
            DisplacementMesh[] current = new DisplacementMesh[keys.Length];

            for (int i = 0; i < keys.Length; ++i)
            {
                tiles[i] = TileRect((int)(uint)keys[i], (int)(keys[i] >> 32), mesh);
                savedRects[i] = new Rectangle(0, 0, tiles[i].Width, tiles[i].Height);
                saved[i] = beforeTiles[keys[i]];
                current[i] = mesh;
            }

            DiskBackedSurface before = DiskBackedSurface.FromPieces(saved, savedRects, tileSize);
            try
            {
                return new HistoryItem(rect, tiles, before, DiskBackedSurface.FromPieces(current, tiles, tileSize));
            }
            catch
            {
                before.Dispose();
                throw;
            }
        }

        // The contents of rect as they were before a whole-mesh change in progress.
        private DiskBackedSurface TakeBefore(DisplacementMesh mesh, Rectangle rect)
        {
            if (rect == mesh.Bounds)
            {
                // it stays in beforeWhole until the item owns it, in case the item can't be made
                return beforeWhole;
            }

            DisplacementMesh piece = NewMesh(rect.Width, rect.Height);
            try
            {
                beforeWhole.ToMemory();
                piece.Copy(beforeWhole.Surface, Point.Empty, rect);

                DiskBackedSurface before = new DiskBackedSurface(piece, true);
                piece = null; // it is the DiskBackedSurface's now
                try
                {
                    before.ToDisk();
                }
                catch
                {
                    before.Dispose();
                    throw;
                }
                return before;
            }
            finally
            {
                if (piece != null)
                {
                    piece.Dispose();
                }
            }
        }

        private void OnError(Exception ex)
        {
            if (!errornotified)
            {
                string errormessage = "There was an error creating the History entry. Further action on this image may or may not result in a corrupted History stack. Restarting this plugin is recommended. Undo and Redo may no longer function as expected.\r\n\r\nException Details:\r\n";
                errormessage += ex.ToString();
                MessageBox.Show(errormessage, "History Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                errornotified = true;
            }
        }

        public bool CanStepBack
        {
            get { return step >= 0; }
        }

        public bool CanStepForward
        {
            get { return step < stack.Count - 1; }
        }

        // StepBack and StepForward throw if the step can't be read back from disk or there is no
        // memory to read it into. The mesh and the position in the history are then unchanged.

        /// <returns>The rect of the mesh that was changed</returns>
        public Rectangle StepBack(DisplacementMesh surface)
        {
            if (!CanStepBack)
            {
                return Rectangle.Empty;
            }

            HistoryItem item = stack[step];
            Apply(item.Before, item, surface);
            step--;
            return item.DeltaRect;
        }

        /// <returns>The rect of the mesh that was changed</returns>
        public Rectangle StepForward(DisplacementMesh surface)
        {
            if (!CanStepForward)
            {
                return Rectangle.Empty;
            }

            HistoryItem item = stack[step + 1];
            Apply(item.After, item, surface);
            step++;
            return item.DeltaRect;
        }

        // delta is the item's Before or After
        private void Apply(DiskBackedSurface delta, HistoryItem item, DisplacementMesh surface)
        {
            delta.ToMemory();

            if (item.Tiles == null)
            {
                surface.Copy(delta.Surface, item.DeltaRect.Location, delta.Bounds);
            }
            else
            {
                for (int i = 0; i < item.Tiles.Length; ++i)
                {
                    Rectangle tile = item.Tiles[i];
                    surface.Copy(delta.Surface, tile.Location, new Rectangle(0, i * tileSize, tile.Width, tile.Height));
                }
            }

            delta.ToDisk();
        }

        #region IDisposable Members

        public void Dispose()
        {
            foreach (HistoryItem hi in stack)
            {
                hi.Dispose();
            }
            stack.Clear();

            lock (sync)
            {
                ClearPending();
            }
        }

        #endregion
    }
}
