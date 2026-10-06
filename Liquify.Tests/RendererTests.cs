using pyrochild.effects.common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace pyrochild.effects.liquify.tests
{
    /// <summary>
    /// The brush, driven the way the dialog drives it: mouse events queued on the renderer, which
    /// works through them on its own thread.
    /// </summary>
    public class RendererTests
    {
        private const float Pressure = 0.5f;
        private const float Density = 0.5f;

        private static LiquifyEventArgs Event(QueuedToolEventType type, MouseButtons button, Point at, int size, LiquifyMode mode, float pressure = Pressure)
        {
            return new LiquifyEventArgs(type, button, at.X, at.Y, size, pressure, Density, mode);
        }

        /// <summary>
        /// Presses at the first point, drags through the rest, releases, and waits for the renderer to
        /// finish. Returns the area the renderer says it changed.
        /// </summary>
        private static Rectangle Stroke(DisplacementMesh mesh, LiquifyMode mode, int size, float pressure, params Point[] path)
        {
            using (ManualResetEventSlim finished = new ManualResetEventSlim())
            {
                LiquifyRenderer renderer = new LiquifyRenderer(mesh);
                renderer.MouseUp += (s, e) => finished.Set();

                renderer.AddEvent(Event(QueuedToolEventType.MouseDown, MouseButtons.Left, path[0], size, mode, pressure));
                for (int i = 1; i < path.Length; ++i)
                {
                    renderer.AddEvent(Event(QueuedToolEventType.MouseMove, MouseButtons.Left, path[i], size, mode, pressure));
                }
                renderer.AddEvent(Event(QueuedToolEventType.MouseUp, MouseButtons.Left, path[path.Length - 1], size, mode, pressure));

                if (!finished.Wait(TimeSpan.FromSeconds(15)))
                {
                    // don't leave a spinning render thread behind to hang the test run
                    renderer.Abort();
                    Assert.Fail("the stroke did not finish");
                }

                Rectangle changed = renderer.PopTotalInvalidRect();
                renderer.Dispose();
                return changed;
            }
        }

        private static List<Point> ChangedVectors(DisplacementMesh mesh)
        {
            List<Point> changed = new List<Point>();
            for (int y = 0; y < mesh.Height; ++y)
            {
                for (int x = 0; x < mesh.Width; ++x)
                {
                    DisplacementVector v = mesh[x, y];
                    if (v.X != 0 || v.Y != 0 || v.Mask != 0)
                    {
                        changed.Add(new Point(x, y));
                    }
                }
            }
            return changed;
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(50)]
        [InlineData(51)]
        public void A_brush_covers_as_many_pixels_across_as_its_size(int size)
        {
            using (DisplacementMesh mesh = new DisplacementMesh(120, 120))
            {
                Stroke(mesh, LiquifyMode.Freeze, size, 1f, new Point(60, 60));

                List<Point> frozen = ChangedVectors(mesh);
                Assert.NotEmpty(frozen);

                int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
                foreach (Point p in frozen)
                {
                    left = Math.Min(left, p.X);
                    top = Math.Min(top, p.Y);
                    right = Math.Max(right, p.X);
                    bottom = Math.Max(bottom, p.Y);
                }

                Assert.Equal(size, right - left + 1);
                Assert.Equal(size, bottom - top + 1);
            }
        }

        private static double DistanceToSegment(Point p, Point a, Point b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double lengthSquared = dx * dx + dy * dy;
            double t = lengthSquared == 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared));
            double cx = a.X + t * dx, cy = a.Y + t * dy;
            return Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        public void A_stroke_with_a_very_small_brush_finishes(int size)
        {
            using (DisplacementMesh mesh = new DisplacementMesh(100, 100))
            {
                Stroke(mesh, LiquifyMode.Push, size, 1f, new Point(30, 30), new Point(60, 45), new Point(70, 70));

                Assert.NotEmpty(ChangedVectors(mesh));
            }
        }

        // total displacement left by a push stroke whose moves carry the given pressures
        private static double PushedAmount(float downPressure, float movePressure)
        {
            using (DisplacementMesh mesh = new DisplacementMesh(200, 120))
            using (ManualResetEventSlim finished = new ManualResetEventSlim())
            {
                LiquifyRenderer renderer = new LiquifyRenderer(mesh);
                renderer.MouseUp += (s, e) => finished.Set();

                renderer.AddEvent(Event(QueuedToolEventType.MouseDown, MouseButtons.Left, new Point(40, 60), 40, LiquifyMode.Push, downPressure));
                foreach (int x in new[] { 70, 100, 130, 160 })
                {
                    renderer.AddEvent(Event(QueuedToolEventType.MouseMove, MouseButtons.Left, new Point(x, 60), 40, LiquifyMode.Push, movePressure));
                }
                renderer.AddEvent(Event(QueuedToolEventType.MouseUp, MouseButtons.Left, new Point(160, 60), 40, LiquifyMode.Push, movePressure));

                if (!finished.Wait(TimeSpan.FromSeconds(15)))
                {
                    renderer.Abort();
                    Assert.Fail("the stroke did not finish");
                }
                renderer.Dispose();

                double total = 0;
                for (int y = 0; y < mesh.Height; ++y)
                {
                    for (int x = 0; x < mesh.Width; ++x)
                    {
                        total += Math.Abs(mesh[x, y].X) + Math.Abs(mesh[x, y].Y);
                    }
                }
                return total;
            }
        }

        [Fact]
        public void A_real_stroke_is_undone_exactly()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(400, 300))
            using (HistoryStack history = new HistoryStack())
            using (ManualResetEventSlim finished = new ManualResetEventSlim())
            {
                // an earlier distortion and some frozen area, for the stroke to go over
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(x * 0.01f, y * -0.02f, (byte)(x < 200 ? 0 : 120)));

                using (DisplacementMesh before = mesh.Clone())
                {
                    LiquifyRenderer renderer = new LiquifyRenderer(mesh);
                    renderer.BeforeMeshChange = rect => history.BeforeChange(mesh, rect);

                    renderer.MouseUp += (s, e) => finished.Set();

                    Point[] path = { new Point(60, 60), new Point(150, 120), new Point(260, 100), new Point(390, 290) };
                    renderer.AddEvent(Event(QueuedToolEventType.MouseDown, MouseButtons.Left, path[0], 70, LiquifyMode.Push, 1f));
                    for (int i = 1; i < path.Length; ++i)
                    {
                        renderer.AddEvent(Event(QueuedToolEventType.MouseMove, MouseButtons.Left, path[i], 70, LiquifyMode.Push, 1f));
                    }
                    renderer.AddEvent(Event(QueuedToolEventType.MouseUp, MouseButtons.Left, path[path.Length - 1], 70, LiquifyMode.Push, 1f));

                    if (!finished.Wait(TimeSpan.FromSeconds(15)))
                    {
                        renderer.Abort();
                        Assert.Fail("the stroke did not finish");
                    }

                    // only the tiles under the stroke were kept, not the whole mesh
                    Assert.InRange(history.PendingVectorCount, 1, 400L * 300 - 1);

                    history.AddHistoryItem(mesh, renderer.PopTotalInvalidRect());
                    renderer.Dispose();

                    Assert.NotNull(TestHelpers.FirstDifference(before, mesh));

                    history.StepBack(mesh);
                    Assert.Null(TestHelpers.FirstDifference(before, mesh));
                }
            }
        }

        [Fact]
        public void A_stroke_stops_where_it_is_told_the_mesh_cannot_be_saved()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(400, 200))
            using (ManualResetEventSlim finished = new ManualResetEventSlim())
            {
                LiquifyRenderer renderer = new LiquifyRenderer(mesh);
                renderer.MouseUp += (s, e) => finished.Set();

                // allowed on the left half of the mesh only
                int refused = 0;
                renderer.BeforeMeshChange = rect =>
                {
                    if (rect.Right <= 200)
                    {
                        return true;
                    }
                    ++refused;
                    return false;
                };

                renderer.AddEvent(Event(QueuedToolEventType.MouseDown, MouseButtons.Left, new Point(40, 100), 40, LiquifyMode.Push, 1f));
                foreach (int x in new[] { 100, 160, 220, 280, 340 })
                {
                    renderer.AddEvent(Event(QueuedToolEventType.MouseMove, MouseButtons.Left, new Point(x, 100), 40, LiquifyMode.Push, 1f));
                }
                renderer.AddEvent(Event(QueuedToolEventType.MouseUp, MouseButtons.Left, new Point(340, 100), 40, LiquifyMode.Push, 1f));

                if (!finished.Wait(TimeSpan.FromSeconds(15)))
                {
                    renderer.Abort();
                    Assert.Fail("the stroke did not finish");
                }
                renderer.Dispose();

                // it drew up to the line, was refused once, and did nothing after that
                List<Point> changed = ChangedVectors(mesh);
                Assert.NotEmpty(changed);
                Assert.All(changed, p => Assert.True(p.X < 200, "changed at " + p));
                Assert.Equal(1, refused);
            }
        }

        [Fact]
        public void Pressure_can_change_along_a_stroke_as_it_does_with_a_pen()
        {
            double firm = PushedAmount(1f, 1f);
            double easingOff = PushedAmount(1f, 0.2f);
            double light = PushedAmount(0.2f, 0.2f);

            // the pressure of each move counts, not just the one the stroke started with
            Assert.True(easingOff < firm * 0.5, "easing off pushed " + easingOff + " against " + firm);
            Assert.InRange(easingOff, light * 0.9, light * 1.6);
        }

        [Fact]
        public void The_half_strength_ring_moves_out_as_density_rises_and_is_gone_at_full_density()
        {
            // strength at distance d is (1 - (d / radius)^2) ^ (2 - 2 * density)
            foreach (float density in new[] { 0f, 0.25f, 0.5f, 0.9f })
            {
                float fraction = LiquifyRenderer.HalfStrengthRadius(density);
                double strength = Math.Pow(1 - fraction * fraction, 2 - 2 * density);
                Assert.InRange(strength, 0.499, 0.501);
            }

            Assert.True(LiquifyRenderer.HalfStrengthRadius(0f) < LiquifyRenderer.HalfStrengthRadius(0.5f));
            Assert.True(LiquifyRenderer.HalfStrengthRadius(0.5f) < LiquifyRenderer.HalfStrengthRadius(0.9f));

            // an even brush never drops to half
            Assert.Equal(0f, LiquifyRenderer.HalfStrengthRadius(1f));
        }

        [Fact]
        public void A_push_stroke_changes_the_mesh_along_its_path_and_nowhere_else()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(300, 200))
            {
                Point from = new Point(60, 100);
                Point to = new Point(220, 100);
                const int size = 40;

                Rectangle reported = Stroke(mesh, LiquifyMode.Push, size, 1f, from, to);

                List<Point> changed = ChangedVectors(mesh);
                Assert.NotEmpty(changed);

                foreach (Point p in changed)
                {
                    Assert.True(DistanceToSegment(p, from, to) <= size / 2 + 1.5, "changed far from the stroke at " + p);
                    Assert.True(reported.Contains(p), "changed outside the reported area at " + p);
                }

                // pushing to the right shows, at each pixel, what was to its left
                Assert.True(mesh[140, 100].X < 0, "expected a leftward offset in the middle of the stroke");
            }
        }

        [Fact]
        public void The_reported_area_stays_inside_the_mesh()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(100, 80))
            {
                Rectangle reported = Stroke(mesh, LiquifyMode.Bloat, 60, 1f, new Point(-10, -10), new Point(110, 90));

                Assert.True(mesh.Bounds.Contains(reported), "reported " + reported);
            }
        }

        [Fact]
        public void Frozen_areas_are_not_distorted()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(200, 200))
            {
                // freeze the left half completely
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(0, 0, (byte)(x < 100 ? 255 : 0)));

                Stroke(mesh, LiquifyMode.Bloat, 80, 1f, new Point(100, 100));

                bool rightChanged = false;
                for (int y = 0; y < 200; ++y)
                {
                    for (int x = 0; x < 200; ++x)
                    {
                        DisplacementVector v = mesh[x, y];
                        if (x < 100)
                        {
                            Assert.True(v.X == 0 && v.Y == 0, "a frozen vector moved at " + x + "," + y);
                            Assert.Equal(255, v.Mask);
                        }
                        else if (v.X != 0 || v.Y != 0)
                        {
                            rightChanged = true;
                        }
                    }
                }

                Assert.True(rightChanged, "the unfrozen half should have been distorted");
            }
        }

        [Fact]
        public void Freeze_and_thaw_change_the_mask_and_not_the_offsets()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(120, 120))
            {
                Stroke(mesh, LiquifyMode.Freeze, 40, 1f, new Point(60, 60));

                Assert.True(mesh[60, 60].Mask > 0, "freezing should raise the mask");
                Assert.Equal(0, mesh[100, 100].Mask);
                Assert.Equal(0f, mesh[60, 60].X);
                Assert.Equal(0f, mesh[60, 60].Y);

                byte frozen = mesh[60, 60].Mask;
                Stroke(mesh, LiquifyMode.Thaw, 40, 1f, new Point(60, 60));

                Assert.True(mesh[60, 60].Mask < frozen, "thawing should lower the mask");
            }
        }

        [Fact]
        public void Reconstruct_moves_offsets_back_towards_zero()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(120, 120))
            {
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(10, -6));

                Stroke(mesh, LiquifyMode.Reconstruct, 60, 0.5f, new Point(60, 60));

                Assert.InRange(mesh[60, 60].X, 0f, 9.99f);
                Assert.InRange(mesh[60, 60].Y, -5.99f, 0f);
                Assert.Equal(10f, mesh[5, 5].X);
            }
        }

        [Fact]
        public void Changing_the_brush_size_during_a_stroke_does_not_take_effect_until_the_next_one()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(400, 300))
            using (ManualResetEventSlim finished = new ManualResetEventSlim())
            {
                Point from = new Point(100, 150);
                Point to = new Point(300, 150);

                LiquifyRenderer renderer = new LiquifyRenderer(mesh);
                renderer.MouseUp += (s, e) => finished.Set();
                renderer.AddEvent(Event(QueuedToolEventType.MouseDown, MouseButtons.Left, from, 30, LiquifyMode.Push, 1f));
                // the size box was changed to something far bigger mid-drag
                renderer.AddEvent(Event(QueuedToolEventType.MouseMove, MouseButtons.Left, to, 280, LiquifyMode.Push, 1f));
                renderer.AddEvent(Event(QueuedToolEventType.MouseUp, MouseButtons.Left, to, 280, LiquifyMode.Push, 1f));

                Assert.True(finished.Wait(TimeSpan.FromSeconds(15)), "the stroke did not finish");
                renderer.Dispose();

                List<Point> changed = ChangedVectors(mesh);
                Assert.NotEmpty(changed);
                foreach (Point p in changed)
                {
                    Assert.True(DistanceToSegment(p, from, to) <= 30 / 2 + 1.5, "the stroke was wider than the brush it started with, at " + p);
                }
            }
        }

        [Fact]
        public void A_click_only_affects_where_it_lands_wherever_the_mouse_was_last_seen()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(400, 400))
            using (ManualResetEventSlim finished = new ManualResetEventSlim())
            {
                Point lastSeen = new Point(40, 40);
                Point click = new Point(300, 300);

                LiquifyRenderer renderer = new LiquifyRenderer(mesh);
                renderer.MouseUp += (s, e) => finished.Set();

                // the mouse hovers, then the view scrolls under it, so the next thing the renderer
                // hears is a press somewhere else entirely
                renderer.AddEvent(Event(QueuedToolEventType.MouseMove, MouseButtons.None, lastSeen, 40, LiquifyMode.Bloat, 1f));
                renderer.AddEvent(Event(QueuedToolEventType.MouseDown, MouseButtons.Left, click, 40, LiquifyMode.Bloat, 1f));
                renderer.AddEvent(Event(QueuedToolEventType.MouseUp, MouseButtons.Left, click, 40, LiquifyMode.Bloat, 1f));

                Assert.True(finished.Wait(TimeSpan.FromSeconds(15)), "the click did not finish");
                renderer.Dispose();

                List<Point> changed = ChangedVectors(mesh);
                Assert.NotEmpty(changed);
                foreach (Point p in changed)
                {
                    Assert.True(DistanceToSegment(p, click, click) <= 40 / 2 + 1.5, "changed away from the click, at " + p);
                }
            }
        }

        [Fact]
        public void Moving_the_mouse_without_a_button_down_changes_nothing()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(100, 100))
            using (ManualResetEventSlim drained = new ManualResetEventSlim())
            {
                int invalidations = 0;
                LiquifyRenderer renderer = new LiquifyRenderer(mesh);
                renderer.Invalidated += (s, e) => Interlocked.Increment(ref invalidations);
                renderer.QueueEmptied += (s, e) => drained.Set();

                for (int i = 0; i < 50; ++i)
                {
                    renderer.AddEvent(Event(QueuedToolEventType.MouseMove, MouseButtons.None, new Point(10 + i, 20 + i), 30, LiquifyMode.Push));
                }

                Assert.True(drained.Wait(TimeSpan.FromSeconds(15)), "the queue was not worked through");
                renderer.Dispose();

                Assert.Equal(0, invalidations);
                Assert.Empty(ChangedVectors(mesh));
            }
        }

        [Fact]
        public void Every_queued_release_is_processed_however_the_timing_falls()
        {
            // An event queued just as the render thread was finishing used to sit unprocessed until
            // the next one arrived. Each round here waits for its own release with nothing after it.
            using (DisplacementMesh mesh = new DisplacementMesh(50, 50))
            using (AutoResetEvent released = new AutoResetEvent(false))
            {
                LiquifyRenderer renderer = new LiquifyRenderer(mesh);
                renderer.MouseUp += (s, e) => released.Set();

                Point at = new Point(25, 25);
                for (int round = 0; round < 300; ++round)
                {
                    renderer.AddEvent(Event(QueuedToolEventType.MouseDown, MouseButtons.Left, at, 4, LiquifyMode.Freeze));
                    renderer.AddEvent(Event(QueuedToolEventType.MouseUp, MouseButtons.Left, at, 4, LiquifyMode.Freeze));

                    Assert.True(released.WaitOne(TimeSpan.FromSeconds(5)), "the release in round " + round + " was never processed");
                }

                renderer.Dispose();
            }
        }

        [Fact]
        public void One_background_thread_does_all_the_rendering_and_ends_with_the_renderer()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(50, 50))
            using (AutoResetEvent released = new AutoResetEvent(false))
            {
                List<Thread> threads = new List<Thread>();
                LiquifyRenderer renderer = new LiquifyRenderer(mesh);
                renderer.MouseUp += (s, e) =>
                {
                    threads.Add(Thread.CurrentThread);
                    released.Set();
                };

                Point at = new Point(25, 25);
                for (int round = 0; round < 5; ++round)
                {
                    renderer.AddEvent(Event(QueuedToolEventType.MouseDown, MouseButtons.Left, at, 4, LiquifyMode.Freeze));
                    renderer.AddEvent(Event(QueuedToolEventType.MouseUp, MouseButtons.Left, at, 4, LiquifyMode.Freeze));
                    Assert.True(released.WaitOne(TimeSpan.FromSeconds(5)), "the release in round " + round + " was never processed");

                    // let the thread go idle between rounds
                    Thread.Sleep(30);
                }

                Assert.Equal(5, threads.Count);
                Assert.All(threads, t => Assert.Same(threads[0], t));
                Assert.NotSame(Thread.CurrentThread, threads[0]);

                // so a hung render can't keep the host from exiting
                Assert.True(threads[0].IsBackground);

                renderer.Dispose();
                Assert.False(threads[0].IsAlive);
            }
        }

        [Fact]
        public void An_exception_while_rendering_is_reported_and_the_stroke_still_ends()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(100, 100))
            using (ManualResetEventSlim released = new ManualResetEventSlim())
            {
                List<Exception> reported = new List<Exception>();
                int invalidations = 0;

                LiquifyRenderer renderer = new LiquifyRenderer(mesh);
                renderer.Error += (s, e) => reported.Add(e.Exception);
                renderer.MouseUp += (s, e) => released.Set();
                renderer.Invalidated += (s, e) =>
                {
                    if (Interlocked.Increment(ref invalidations) == 1)
                    {
                        throw new InvalidOperationException("went wrong");
                    }
                };

                renderer.AddEvent(Event(QueuedToolEventType.MouseDown, MouseButtons.Left, new Point(30, 50), 20, LiquifyMode.Bloat, 1f));
                renderer.AddEvent(Event(QueuedToolEventType.MouseMove, MouseButtons.Left, new Point(70, 50), 20, LiquifyMode.Bloat, 1f));
                renderer.AddEvent(Event(QueuedToolEventType.MouseUp, MouseButtons.Left, new Point(70, 50), 20, LiquifyMode.Bloat, 1f));

                Assert.True(released.Wait(TimeSpan.FromSeconds(15)), "the release was never processed");
                renderer.Dispose();

                Exception only = Assert.Single(reported);
                Assert.Equal("went wrong", only.Message);

                // the move after the one that failed was still rendered
                Assert.True(invalidations >= 2, "nothing was rendered after the exception");
            }
        }

        [Fact]
        public void Nothing_runs_after_the_renderer_is_disposed()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(60, 60))
            {
                LiquifyRenderer renderer = new LiquifyRenderer(mesh);
                renderer.Dispose();

                renderer.AddEvent(Event(QueuedToolEventType.MouseDown, MouseButtons.Left, new Point(30, 30), 20, LiquifyMode.Bloat, 1f));
                renderer.AddEvent(Event(QueuedToolEventType.MouseUp, MouseButtons.Left, new Point(30, 30), 20, LiquifyMode.Bloat, 1f));
                Thread.Sleep(100);

                Assert.Equal(0, renderer.GetQueueSize());
                Assert.Empty(ChangedVectors(mesh));
            }
        }
    }
}
