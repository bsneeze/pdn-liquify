using PaintDotNet;
using System;
using System.Drawing;
using Xunit;

namespace pyrochild.effects.liquify.tests
{
    public class MeshTests
    {
        [Fact]
        public void An_untouched_mesh_renders_the_source_unchanged()
        {
            using (Surface source = TestHelpers.PositionSurface(200, 150))
            using (Surface result = new Surface(200, 150))
            using (DisplacementMesh mesh = new DisplacementMesh(200, 150))
            {
                mesh.Render(result, source, source.Bounds);

                for (int y = 0; y < 150; ++y)
                {
                    for (int x = 0; x < 200; ++x)
                    {
                        Assert.True(source[x, y] == result[x, y], "pixel " + x + "," + y);
                    }
                }
            }
        }

        [Fact]
        public unsafe void Pixels_read_in_place_render_the_same_as_a_copy_of_them()
        {
            using (Surface source = TestHelpers.PositionSurface(120, 90))
            using (Surface expected = new Surface(120, 90))
            using (Surface result = new Surface(120, 90))
            using (DisplacementMesh mesh = new DisplacementMesh(120, 90))
            {
                // stretched in places and squeezed in others, so both of RenderSupersampled's paths run
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(x * 0.7f - 30, (y - 45) * -0.4f));

                BitmapSurface inPlace = new BitmapSurface((ColorBgra*)source.Scan0.VoidStar, source.Width, source.Height, source.Stride);

                mesh.RenderSupersampled(expected, source, source.Bounds);
                mesh.RenderSupersampled(result, inPlace, source.Bounds);

                for (int y = 0; y < 90; ++y)
                {
                    for (int x = 0; x < 120; ++x)
                    {
                        Assert.True(expected[x, y] == result[x, y], "pixel " + x + "," + y);
                    }
                }
            }
        }

        [Fact]
        public unsafe void A_rectangle_can_be_rendered_into_memory_that_holds_only_that_rectangle()
        {
            Rectangle rect = new Rectangle(30, 20, 50, 40);

            using (Surface source = TestHelpers.PositionSurface(120, 90))
            using (Surface expected = new Surface(120, 90))
            using (Surface result = new Surface(rect.Width + 2, rect.Height + 2)) // with a border to catch overruns
            using (DisplacementMesh mesh = new DisplacementMesh(120, 90))
            {
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(x * 0.7f - 30, (y - 45) * -0.4f));

                ColorBgra untouched = ColorBgra.FromBgra(1, 2, 3, 4);
                result.Fill(untouched);

                BitmapSurface output = BitmapSurface.ForRect((ColorBgra*)result.GetPointPointer(1, 1), rect, result.Stride, source.Size);

                mesh.RenderSupersampled(expected, source, rect);
                mesh.RenderSupersampled(output, source, rect);

                for (int y = 0; y < result.Height; ++y)
                {
                    for (int x = 0; x < result.Width; ++x)
                    {
                        bool border = x == 0 || y == 0 || x == result.Width - 1 || y == result.Height - 1;
                        ColorBgra wanted = border ? untouched : expected[rect.X + x - 1, rect.Y + y - 1];
                        Assert.True(wanted == result[x, y], "pixel " + x + "," + y);
                    }
                }
            }
        }

        [Fact]
        public void Resizing_into_a_mesh_replaces_its_distortion_and_leaves_its_mask()
        {
            using (DisplacementMesh small = new DisplacementMesh(20, 15))
            using (DisplacementMesh target = new DisplacementMesh(50, 45))
            using (DisplacementMesh expected = Resized(small, 50, 45))
            {
                // old values that must all be overwritten
                TestHelpers.Fill(target, (x, y) => new DisplacementVector(99, -99, (byte)(x + y)));

                small.ResizeInto(target);

                for (int y = 0; y < 45; ++y)
                {
                    for (int x = 0; x < 50; ++x)
                    {
                        Assert.Equal(expected[x, y].X, target[x, y].X);
                        Assert.Equal(expected[x, y].Y, target[x, y].Y);
                        Assert.Equal((byte)(x + y), target[x, y].Mask);
                    }
                }
            }
        }

        private static DisplacementMesh Resized(DisplacementMesh mesh, int width, int height)
        {
            TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(x * 0.5f - 3, y * -0.25f + 2));
            return mesh.Resize(new Size(width, height));
        }

        [Fact]
        public void A_constant_offset_pulls_pixels_from_that_far_away()
        {
            using (Surface source = TestHelpers.PositionSurface(100, 80))
            using (Surface result = new Surface(100, 80))
            using (DisplacementMesh mesh = new DisplacementMesh(100, 80))
            {
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(3, -2));

                mesh.Render(result, source, source.Bounds);

                // away from the edges, where the offset would point outside the image
                for (int y = 5; y < 75; ++y)
                {
                    for (int x = 5; x < 90; ++x)
                    {
                        Assert.True(source[x + 3, y - 2] == result[x, y], "pixel " + x + "," + y);
                    }
                }
            }
        }

        [Fact]
        public void Only_the_requested_rectangle_is_rendered()
        {
            using (Surface source = TestHelpers.PositionSurface(60, 60))
            using (Surface result = new Surface(60, 60))
            using (DisplacementMesh mesh = new DisplacementMesh(60, 60))
            {
                ColorBgra untouched = ColorBgra.FromBgra(1, 2, 3, 4);
                result.Fill(untouched);

                mesh.Render(result, source, new Rectangle(10, 20, 15, 5));

                Assert.True(result[9, 20] == untouched);
                Assert.True(result[25, 20] == untouched);
                Assert.True(result[10, 19] == untouched);
                Assert.True(result[10, 25] == untouched);
                Assert.True(result[10, 20] == source[10, 20]);
                Assert.True(result[24, 24] == source[24, 24]);
            }
        }

        [Theory]
        [InlineData(1, 40)]
        [InlineData(40, 1)]
        [InlineData(1, 1)]
        public void One_pixel_wide_or_tall_meshes_can_be_sampled_and_rendered(int width, int height)
        {
            using (Surface source = TestHelpers.PositionSurface(width, height))
            using (Surface result = new Surface(width, height))
            using (DisplacementMesh mesh = new DisplacementMesh(width, height))
            {
                TestHelpers.Set(mesh, width - 1, height - 1, 5, 7);

                DisplacementVector sample = mesh.GetBilinearSample(width - 1, height - 1);
                Assert.Equal(5f, sample.X);
                Assert.Equal(7f, sample.Y);

                // just outside on every side is clamped to the nearest vector, not read out of bounds
                mesh.GetBilinearSample(-3, -3);
                mesh.GetBilinearSample(width + 3, height + 3);

                mesh.ClearOffsets();
                mesh.Render(result, source, source.Bounds);
                Assert.True(result[0, 0] == source[0, 0]);
                Assert.True(result[width - 1, height - 1] == source[width - 1, height - 1]);
            }
        }

        [Fact]
        public void Sampling_between_vectors_interpolates()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(4, 4))
            {
                TestHelpers.Set(mesh, 1, 1, 10, 0);
                TestHelpers.Set(mesh, 2, 1, 20, 0);
                TestHelpers.Set(mesh, 1, 2, 10, 8);
                TestHelpers.Set(mesh, 2, 2, 20, 8);

                DisplacementVector sample = mesh.GetBilinearSample(1.5f, 1.25f);

                Assert.Equal(15f, sample.X, 3);
                Assert.Equal(2f, sample.Y, 3);
            }
        }

        [Fact]
        public void Resizing_scales_the_offsets_with_the_mesh()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(10, 10))
            {
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(4, 2));

                using (DisplacementMesh bigger = mesh.Resize(new Size(20, 30)))
                {
                    Assert.Equal(new Size(20, 30), bigger.Size);
                    Assert.Equal(8f, bigger[10, 15].X, 3);
                    Assert.Equal(6f, bigger[10, 15].Y, 3);
                }
            }
        }

        [Fact]
        public void Mask_and_offset_operations_leave_each_other_alone()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(200, 200))
            {
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(x * 0.5f, -y, (byte)(x % 256)));

                mesh.InvertMask();
                Assert.Equal(255 - 37, mesh[37, 5].Mask);
                Assert.Equal(18.5f, mesh[37, 5].X);

                mesh.ClearMask();
                Assert.Equal(0, mesh[37, 5].Mask);
                Assert.Equal(-5f, mesh[37, 5].Y);

                TestHelpers.Set(mesh, 3, 3, 9, 9, 200);
                mesh.ClearOffsets();
                Assert.Equal(0f, mesh[3, 3].X);
                Assert.Equal(0f, mesh[3, 3].Y);
                Assert.Equal(200, mesh[3, 3].Mask);
            }
        }

        [Fact]
        public void The_mask_tint_only_shows_with_a_visible_mask_color()
        {
            using (Surface source = TestHelpers.PositionSurface(20, 20))
            using (Surface result = new Surface(20, 20))
            using (DisplacementMesh mesh = new DisplacementMesh(20, 20))
            {
                TestHelpers.Set(mesh, 5, 5, 0, 0, 255);

                mesh.Render(result, source, source.Bounds, ColorBgra.Red);
                Assert.False(result[5, 5] == source[5, 5], "a frozen pixel should be tinted");
                Assert.True(result[6, 5] == source[6, 5], "an unfrozen pixel should not be");

                mesh.Render(result, source, source.Bounds, ColorBgra.Red.NewAlpha(0));
                Assert.True(result[5, 5] == source[5, 5], "a transparent mask color should leave no tint");
            }
        }

        [Fact]
        public void Supersampling_matches_the_plain_render_where_nothing_is_squeezed()
        {
            using (Surface source = TestHelpers.PositionSurface(120, 90))
            using (Surface plain = new Surface(120, 90))
            using (Surface supersampled = new Surface(120, 90))
            using (DisplacementMesh mesh = new DisplacementMesh(120, 90))
            {
                // a shift, which moves pixels but doesn't compress anything
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(2.5f, 1.25f));

                mesh.Render(plain, source, source.Bounds);
                mesh.RenderSupersampled(supersampled, source, source.Bounds);

                for (int y = 0; y < 90; ++y)
                {
                    for (int x = 0; x < 120; ++x)
                    {
                        Assert.True(plain[x, y] == supersampled[x, y], "pixel " + x + "," + y);
                    }
                }
            }
        }

        [Fact]
        public void Supersampling_averages_the_source_pixels_a_squeezed_pixel_covers()
        {
            const int width = 300;
            const int height = 20;

            using (Surface source = new Surface(width, height))
            using (Surface plain = new Surface(width, height))
            using (Surface supersampled = new Surface(width, height))
            using (DisplacementMesh mesh = new DisplacementMesh(width, height))
            {
                // vertical stripes three pixels across: 0, 90, 180, 0, 90, 180, ...
                for (int y = 0; y < height; ++y)
                {
                    for (int x = 0; x < width; ++x)
                    {
                        byte v = (byte)(90 * (x % 3));
                        source[x, y] = ColorBgra.FromBgra(v, v, v, 255);
                    }
                }

                // output pixel x shows source pixel 3x: the image is squeezed to a third of its width
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(2 * x, 0));

                mesh.Render(plain, source, source.Bounds);
                mesh.RenderSupersampled(supersampled, source, source.Bounds);

                for (int x = 2; x < 90; ++x)
                {
                    // one sample lands on every third column, which is always a 0 stripe
                    Assert.Equal(0, plain[x, 10].R);

                    // three samples land on one column of each stripe
                    Assert.InRange(supersampled[x, 10].R, 88, 92);
                    Assert.Equal(255, supersampled[x, 10].A);
                }
            }
        }

        [Fact]
        public unsafe void The_grid_is_drawn_on_grid_lines_and_nowhere_else()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(100, 100))
            {
                uint[] row = new uint[100];
                for (int i = 0; i < row.Length; ++i)
                {
                    row[i] = 0xFF202020; // dark, so a grid pixel is lightened
                }

                fixed (uint* pixels = row)
                {
                    // row 5 at 100% zoom: only the vertical lines, every 10 pixels, cross it
                    mesh.DrawGridRow((IntPtr)pixels, 0, 5, 100, 1f, 10f, 0.5f);
                }

                for (int x = 0; x < 100; ++x)
                {
                    bool onLine = x % 10 == 0;
                    Assert.True((row[x] != 0xFF202020) == onLine, "pixel " + x + " was " + row[x].ToString("X8"));
                }
            }
        }

        [Fact]
        public unsafe void The_grid_follows_the_distortion()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(100, 100))
            {
                // everything shows the source three pixels to the right, so the lines move three left
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(3, 0));

                uint[] row = new uint[100];
                fixed (uint* pixels = row)
                {
                    mesh.DrawGridRow((IntPtr)pixels, 0, 5, 100, 1f, 10f, 0.5f);
                }

                Assert.NotEqual(0u, row[7]);
                Assert.NotEqual(0u, row[17]);
                Assert.Equal(0u, row[10]);
                Assert.Equal(0u, row[20]);
            }
        }
    }
}
