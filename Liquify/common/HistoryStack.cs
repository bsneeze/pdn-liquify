using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace pyrochild.effects.liquify
{
    public class HistoryStack : IDisposable
    {
        List<HistoryItem> stack;
        int step; // index of the last applied item, -1 if there is none
        bool errornotified;

        // The mesh as of the current history step. A stroke's rect is only known once it has finished,
        // so this is where the "before" half of its history item comes from.
        DisplacementMesh committed;

        public HistoryStack(DisplacementMesh mesh)
        {
            stack = new List<HistoryItem>();
            step = -1;
            committed = mesh.Clone();
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

        public void AddHistoryItem(DisplacementMesh mesh, Rectangle bounds)
        {
            Rectangle rect = Rectangle.Intersect(mesh.Bounds, bounds);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            try
            {
                RemoveRedoItems();
                stack.Add(new HistoryItem(committed, mesh, rect));
                step++;
                committed.Copy(mesh, rect.Location, rect);
            }
            catch (Exception ex)
            {
                OnError(ex);
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

        /// <returns>The rect of the mesh that was changed</returns>
        public Rectangle StepBack(DisplacementMesh surface)
        {
            if (!CanStepBack)
            {
                return Rectangle.Empty;
            }

            HistoryItem item = stack[step];
            Apply(item.Before, item.DeltaRect, surface);
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

            step++;
            HistoryItem item = stack[step];
            Apply(item.After, item.DeltaRect, surface);
            return item.DeltaRect;
        }

        private void Apply(DiskBackedSurface delta, Rectangle rect, DisplacementMesh surface)
        {
            delta.ToMemory();
            surface.Copy(delta.Surface, rect.Location, delta.Bounds);
            committed.Copy(delta.Surface, rect.Location, delta.Bounds);
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
            committed.Dispose();
        }

        #endregion
    }
}
