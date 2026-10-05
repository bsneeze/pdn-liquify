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
            using (HistoryStack history = new HistoryStack(mesh))
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

        [Fact]
        public void Undoing_an_edit_leaves_a_freeze_mask_made_earlier_in_place()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(40, 40))
            using (HistoryStack history = new HistoryStack(mesh))
            {
                Rectangle frozen = new Rectangle(10, 10, 10, 10);
                Edit(mesh, frozen, 0, 255);
                history.AddHistoryItem(mesh, frozen);

                // a later stroke that overlaps the frozen area
                Rectangle stroke = new Rectangle(15, 15, 20, 20);
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
            using (HistoryStack history = new HistoryStack(mesh))
            {
                Rectangle rect = new Rectangle(2, 2, 5, 5);

                Edit(mesh, rect, 10);
                history.AddHistoryItem(mesh, rect);
                Edit(mesh, rect, 20);
                history.AddHistoryItem(mesh, rect);

                history.StepBack(mesh);
                Assert.True(history.CanStepForward);

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
            using (HistoryStack history = new HistoryStack(mesh))
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
            using (HistoryStack history = new HistoryStack(mesh))
            {
                Edit(mesh, new Rectangle(15, 15, 5, 5), 9);
                history.AddHistoryItem(mesh, new Rectangle(15, 15, 40, 40));

                Assert.Equal(new Rectangle(15, 15, 5, 5), history.StepBack(mesh));
                Assert.Equal(0f, mesh[19, 19].X);
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
