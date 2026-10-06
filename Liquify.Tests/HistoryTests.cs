using System.Drawing;
using System.IO;
using Xunit;

namespace pyrochild.effects.liquify.tests
{
    public class HistoryTests
    {
        // stands in for a brush stroke: changes every vector in the rect
        private static void Edit(DisplacementMesh mesh, Rectangle rect, float value, byte mask = 0)
        {
            for (int y = rect.Top; y < rect.Bottom; ++y)
            {
                for (int x = rect.Left; x < rect.Right; ++x)
                {
                    TestHelpers.Set(mesh, x, y, value + x, value - y, mask);
                }
            }
        }

        [Fact]
        public void Undo_and_redo_step_through_overlapping_edits_exactly()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(64, 48))
            using (HistoryStack history = new HistoryStack())
            {
                Rectangle[] rects =
                {
                    new Rectangle(5, 5, 30, 20),
                    new Rectangle(20, 10, 30, 30),
                    new Rectangle(0, 0, 64, 48),
                    new Rectangle(25, 15, 4, 4)
                };

                // the mesh as it was before any edit, then after each one
                DisplacementMesh[] states = new DisplacementMesh[rects.Length + 1];
                states[0] = mesh.Clone();
                for (int i = 0; i < rects.Length; ++i)
                {
                    history.BeforeChange(mesh, rects[i]);
                    Edit(mesh, rects[i], (i + 1) * 100, (byte)(i * 60));
                    history.AddHistoryItem(mesh, rects[i]);
                    states[i + 1] = mesh.Clone();
                }

                Assert.False(history.CanStepForward);

                for (int i = rects.Length; i > 0; --i)
                {
                    Assert.True(history.CanStepBack);
                    Rectangle changed = history.StepBack(mesh);
                    Assert.Equal(rects[i - 1], changed);
                    Assert.Null(TestHelpers.FirstDifference(states[i - 1], mesh));
                }

                Assert.False(history.CanStepBack);

                for (int i = 1; i <= rects.Length; ++i)
                {
                    Assert.True(history.CanStepForward);
                    Rectangle changed = history.StepForward(mesh);
                    Assert.Equal(rects[i - 1], changed);
                    Assert.Null(TestHelpers.FirstDifference(states[i], mesh));
                }

                foreach (DisplacementMesh state in states)
                {
                    state.Dispose();
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void A_change_that_failed_part_way_can_be_put_back(bool wholeMesh)
        {
            using (DisplacementMesh mesh = new DisplacementMesh(200, 150))
            using (HistoryStack history = new HistoryStack())
            {
                Edit(mesh, mesh.Bounds, 7, 40);
                Rectangle rect = wholeMesh ? mesh.Bounds : new Rectangle(30, 20, 100, 90);

                using (DisplacementMesh original = mesh.Clone())
                {
                    Assert.True(history.BeforeChange(mesh, rect));
                    Edit(mesh, rect, 500, 200);

                    history.RevertChange(mesh);

                    Assert.Null(TestHelpers.FirstDifference(original, mesh));
                    Assert.False(history.CanStepBack);
                    Assert.Equal(0, history.PendingVectorCount);
                }
            }
        }

        [Fact]
        public void Undoing_an_edit_leaves_a_freeze_mask_made_earlier_in_place()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(40, 40))
            using (HistoryStack history = new HistoryStack())
            {
                Rectangle frozen = new Rectangle(10, 10, 10, 10);
                history.BeforeChange(mesh, frozen);
                Edit(mesh, frozen, 0, 255);
                history.AddHistoryItem(mesh, frozen);

                // a later stroke that overlaps the frozen area
                Rectangle stroke = new Rectangle(15, 15, 20, 20);
                history.BeforeChange(mesh, stroke);
                for (int y = stroke.Top; y < stroke.Bottom; ++y)
                {
                    for (int x = stroke.Left; x < stroke.Right; ++x)
                    {
                        TestHelpers.Set(mesh, x, y, 50, 50, mesh[x, y].Mask);
                    }
                }
                history.AddHistoryItem(mesh, stroke);

                history.StepBack(mesh);

                Assert.Equal(255, mesh[17, 17].Mask);
                Assert.Equal(255, mesh[12, 12].Mask);
                Assert.Equal(0, mesh[30, 30].Mask);
            }
        }

        [Fact]
        public void A_new_edit_after_an_undo_discards_what_could_have_been_redone()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(20, 20))
            using (HistoryStack history = new HistoryStack())
            {
                Rectangle rect = new Rectangle(2, 2, 5, 5);

                history.BeforeChange(mesh, rect);
                Edit(mesh, rect, 10);
                history.AddHistoryItem(mesh, rect);
                history.BeforeChange(mesh, rect);
                Edit(mesh, rect, 20);
                history.AddHistoryItem(mesh, rect);

                history.StepBack(mesh);
                Assert.True(history.CanStepForward);

                history.BeforeChange(mesh, rect);
                Edit(mesh, rect, 30);
                history.AddHistoryItem(mesh, rect);

                Assert.False(history.CanStepForward);

                history.StepBack(mesh);
                Assert.Equal(10f + 3, mesh[3, 3].X);
                history.StepBack(mesh);
                Assert.Equal(0f, mesh[3, 3].X);
                Assert.False(history.CanStepBack);
            }
        }

        [Fact]
        public void An_edit_that_touches_nothing_adds_no_step()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(20, 20))
            using (HistoryStack history = new HistoryStack())
            {
                history.AddHistoryItem(mesh, Rectangle.Empty);
                history.AddHistoryItem(mesh, new Rectangle(500, 500, 10, 10));

                Assert.False(history.CanStepBack);
            }
        }

        [Fact]
        public void An_edit_that_runs_off_the_mesh_is_recorded_for_the_part_inside()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(20, 20))
            using (HistoryStack history = new HistoryStack())
            {
                history.BeforeChange(mesh, new Rectangle(15, 15, 40, 40));
                Edit(mesh, new Rectangle(15, 15, 5, 5), 9);
                history.AddHistoryItem(mesh, new Rectangle(15, 15, 40, 40));

                Assert.Equal(new Rectangle(15, 15, 5, 5), history.StepBack(mesh));
                Assert.Equal(0f, mesh[19, 19].X);
            }
        }

        [Fact]
        public void Only_the_touched_part_of_the_mesh_is_held_while_a_change_is_in_progress()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(1000, 800))
            using (HistoryStack history = new HistoryStack())
            {
                Assert.Equal(0, history.PendingVectorCount);

                // a brush dab, and the same place again
                history.BeforeChange(mesh, new Rectangle(300, 300, 50, 50));
                long afterOneDab = history.PendingVectorCount;
                Assert.InRange(afterOneDab, 50 * 50, 4 * 64 * 64);

                history.BeforeChange(mesh, new Rectangle(310, 305, 30, 30));
                Assert.Equal(afterOneDab, history.PendingVectorCount);

                // nothing is kept once the change has been recorded, or called off
                history.AddHistoryItem(mesh, new Rectangle(300, 300, 50, 50));
                Assert.Equal(0, history.PendingVectorCount);

                history.BeforeChange(mesh, new Rectangle(0, 0, 10, 10));
                history.CancelChange();
                Assert.Equal(0, history.PendingVectorCount);
                history.AddHistoryItem(mesh, Rectangle.Empty);
            }
        }

        [Fact]
        public void A_change_announced_bit_by_bit_is_undone_exactly()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(300, 200))
            using (HistoryStack history = new HistoryStack())
            {
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(x * 0.5f, y * 0.25f, (byte)(x ^ y)));

                using (DisplacementMesh before = mesh.Clone())
                {
                    // the way a stroke goes: one dab at a time, each announced just before it is made,
                    // overlapping the last and running off the edge of the mesh
                    Rectangle total = Rectangle.Empty;
                    for (int i = 0; i < 12; ++i)
                    {
                        Rectangle dab = new Rectangle(40 + i * 23, 150 - i * 9, 45, 45);
                        history.BeforeChange(mesh, dab);
                        Edit(mesh, Rectangle.Intersect(dab, mesh.Bounds), i * 3 + 1, (byte)(i * 20));
                        total = total.IsEmpty ? dab : Rectangle.Union(total, dab);
                    }

                    // recorded with a larger area than was touched, as the renderer's invalid rect is
                    total.Inflate(10, 10);
                    history.AddHistoryItem(mesh, total);

                    using (DisplacementMesh after = mesh.Clone())
                    {
                        history.StepBack(mesh);
                        Assert.Null(TestHelpers.FirstDifference(before, mesh));

                        history.StepForward(mesh);
                        Assert.Null(TestHelpers.FirstDifference(after, mesh));
                    }
                }
            }
        }

        // makes a history stack behave as if memory ran out after it had been given so many pieces
        private static void RunOutOfMemoryAfter(HistoryStack history, int pieces)
        {
            int left = pieces;
            history.NewMesh = (width, height) =>
            {
                if (left-- <= 0)
                {
                    throw new System.OutOfMemoryException();
                }
                return new DisplacementMesh(width, height);
            };
        }

        [Fact]
        public void When_memory_runs_out_mid_change_the_caller_is_told_before_anything_is_overwritten()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(400, 300))
            using (HistoryStack history = new HistoryStack())
            {
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(x, y, 7));

                using (DisplacementMesh start = mesh.Clone())
                {
                    RunOutOfMemoryAfter(history, 2);

                    // two dabs get through; each fits in one tile
                    Rectangle first = new Rectangle(10, 10, 30, 30);
                    Rectangle second = new Rectangle(70, 10, 30, 30);
                    Assert.True(history.BeforeChange(mesh, first));
                    Edit(mesh, first, 500);
                    Assert.True(history.BeforeChange(mesh, second));
                    Edit(mesh, second, 600);

                    // the third is refused, so the caller leaves the mesh alone there
                    Assert.False(history.BeforeChange(mesh, new Rectangle(200, 200, 30, 30)));
                    Assert.Equal(HistoryFailure.Memory, history.LastFailure);

                    // what did get done is still a proper undo step
                    history.NewMesh = (width, height) => new DisplacementMesh(width, height);
                    Assert.True(history.AddHistoryItem(mesh, new Rectangle(0, 0, 400, 300)));
                    history.StepBack(mesh);
                    Assert.Null(TestHelpers.FirstDifference(start, mesh));
                }
            }
        }

        [Fact]
        public void A_change_that_cannot_be_recorded_is_put_back()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(400, 300))
            using (HistoryStack history = new HistoryStack())
            {
                // an earlier step, and one undone, to see that neither is disturbed
                Rectangle earlier = new Rectangle(300, 200, 20, 20);
                history.BeforeChange(mesh, earlier);
                Edit(mesh, earlier, 50);
                history.AddHistoryItem(mesh, earlier);
                history.BeforeChange(mesh, earlier);
                Edit(mesh, earlier, 60);
                history.AddHistoryItem(mesh, earlier);
                history.StepBack(mesh);

                using (DisplacementMesh start = mesh.Clone())
                {
                    Rectangle stroke = new Rectangle(20, 20, 150, 90);
                    Assert.True(history.BeforeChange(mesh, stroke));
                    Edit(mesh, stroke, 900, 200);

                    // no memory left for putting the step together
                    RunOutOfMemoryAfter(history, 0);
                    Assert.False(history.AddHistoryItem(mesh, stroke));
                    Assert.Equal(HistoryFailure.Memory, history.LastFailure);

                    Assert.Null(TestHelpers.FirstDifference(start, mesh));
                    Assert.Equal(0, history.PendingVectorCount);

                    // the steps that were there are as they were, including the one that can be redone
                    Assert.True(history.CanStepForward);
                    history.StepForward(mesh);
                    Assert.Equal(60f + 305, mesh[305, 205].X);
                }
            }
        }

        private static int TempFileCount()
        {
            return Directory.GetFiles(Path.GetTempPath(), "*.tmp").Length;
        }

        [Fact]
        public void A_stroke_that_cannot_be_written_to_disk_is_put_back()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(300, 200))
            using (HistoryStack history = new HistoryStack())
            {
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(x * 2, y * 3, (byte)x));

                using (DisplacementMesh start = mesh.Clone())
                {
                    Rectangle stroke = new Rectangle(30, 40, 120, 80);
                    Assert.True(history.BeforeChange(mesh, stroke)); // held in memory: no disk needed yet
                    Edit(mesh, stroke, 700, 90);

                    DiskBackedSurface.TestFailWritesOnThisThread = true;
                    try
                    {
                        Assert.False(history.AddHistoryItem(mesh, stroke));
                    }
                    finally
                    {
                        DiskBackedSurface.TestFailWritesOnThisThread = false;
                    }

                    Assert.Equal(HistoryFailure.Disk, history.LastFailure);
                    Assert.Null(TestHelpers.FirstDifference(start, mesh));
                    Assert.False(history.CanStepBack);
                    Assert.Equal(0, history.PendingVectorCount);

                    // and history carries on working once the disk does
                    Assert.True(history.BeforeChange(mesh, stroke));
                    Edit(mesh, stroke, 800);
                    Assert.True(history.AddHistoryItem(mesh, stroke));
                    history.StepBack(mesh);
                    Assert.Null(TestHelpers.FirstDifference(start, mesh));
                }
            }
        }

        [Fact]
        public void A_whole_mesh_change_is_refused_or_put_back_when_the_disk_cannot_be_written()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(120, 90))
            using (HistoryStack history = new HistoryStack())
            {
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(x, -y, (byte)y));

                using (DisplacementMesh start = mesh.Clone())
                {
                    // the "before" of a whole-mesh change goes straight to disk, so it is refused up front
                    DiskBackedSurface.TestFailWritesOnThisThread = true;
                    try
                    {
                        Assert.False(history.BeforeChange(mesh, mesh.Bounds));
                    }
                    finally
                    {
                        DiskBackedSurface.TestFailWritesOnThisThread = false;
                    }
                    Assert.Equal(HistoryFailure.Disk, history.LastFailure);
                    history.CancelChange();

                    // if the disk fills up between the two halves, the change is read back and undone
                    Assert.True(history.BeforeChange(mesh, mesh.Bounds));
                    Edit(mesh, mesh.Bounds, 40, 255);

                    DiskBackedSurface.TestFailWritesOnThisThread = true;
                    try
                    {
                        Assert.False(history.AddHistoryItem(mesh, mesh.Bounds));
                    }
                    finally
                    {
                        DiskBackedSurface.TestFailWritesOnThisThread = false;
                    }

                    Assert.Null(TestHelpers.FirstDifference(start, mesh));
                    Assert.False(history.CanStepBack);
                }
            }
        }

        [Fact]
        public void A_failed_write_leaves_no_file_behind()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(40, 30))
            {
                DiskBackedSurface.TestFailWritesOnThisThread = true;
                try
                {
                    // other tests make and delete temp files at the same time, so look for a steady
                    // climb over many failures, not an exact count
                    int before = TempFileCount();
                    for (int i = 0; i < 40; ++i)
                    {
                        Assert.Throws<IOException>(() => DiskBackedSurface.FromRect(mesh, mesh.Bounds));
                    }
                    Assert.True(TempFileCount() < before + 20, "failed writes are leaving their files behind");
                }
                finally
                {
                    DiskBackedSurface.TestFailWritesOnThisThread = false;
                }
            }
        }

        [Fact]
        public void A_stored_piece_of_mesh_reads_back_exactly_and_cleans_up_its_file()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(50, 40))
            {
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(x * 0.25f, y * -1.5f, (byte)(x + y)));
                Rectangle rect = new Rectangle(7, 9, 20, 15);

                DiskBackedSurface stored = DiskBackedSurface.FromRect(mesh, rect);
                string file = stored.BackingFilePath;
                Assert.True(File.Exists(file));
                Assert.Equal(rect.Size, stored.Size);

                // it is held open, which is what lets Windows remove it if the program dies
                Assert.Throws<IOException>(() => File.Delete(file));

                // and can be read more than once
                stored.ToMemory();
                stored.ToDisk();
                stored.ToMemory();
                for (int y = 0; y < rect.Height; ++y)
                {
                    for (int x = 0; x < rect.Width; ++x)
                    {
                        DisplacementVector expected = mesh[rect.X + x, rect.Y + y];
                        DisplacementVector actual = stored.Surface[x, y];
                        Assert.True(expected.X == actual.X && expected.Y == actual.Y && expected.Mask == actual.Mask,
                            "vector " + x + "," + y);
                    }
                }

                stored.Dispose();
                Assert.False(File.Exists(file));
            }
        }
    }
}
