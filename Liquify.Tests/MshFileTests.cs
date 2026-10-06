using System;
using System.Drawing;
using System.IO;
using Xunit;

namespace pyrochild.effects.liquify.tests
{
    /// <summary>
    /// .msh files are interchangeable with Photoshop's Liquify mesh files, so the layout is fixed.
    /// These tests spell the layout out byte by byte. If one fails, the file format has changed, and
    /// the fix is almost certainly to change the code back, not the test.
    /// </summary>
    public class MshFileTests
    {
        private static readonly byte[] Header =
        {
            0, 0, 0, 2,
            (byte)'y', (byte)'f', (byte)'q', (byte)'L', (byte)'h', (byte)'s', (byte)'e', (byte)'M',
            2, 0, 0, 0
        };

        private static byte[] Save(DisplacementMesh mesh)
        {
            MemoryStream stream = new MemoryStream();
            mesh.Save(stream); // closes the stream; ToArray still works
            return stream.ToArray();
        }

        private static byte[] BuildFile(int width, int height, Func<int, int, PointF> offset)
        {
            MemoryStream stream = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Header);
                writer.Write(width);
                writer.Write(height);
                for (int y = 0; y < height; ++y)
                {
                    for (int x = 0; x < width; ++x)
                    {
                        PointF p = offset(x, y);
                        writer.Write(p.X);
                        writer.Write(p.Y);
                    }
                }
                writer.Write(0L);
            }
            return stream.ToArray();
        }

        [Fact]
        public void A_saved_file_has_exactly_the_documented_layout()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(3, 2))
            {
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(x + 0.5f, -(y * 10 + x), 77));

                byte[] file = Save(mesh);

                // 16-byte header, width, height, an X and a Y float per vector, and 8 zero bytes
                Assert.Equal(16 + 4 + 4 + 3 * 2 * 8 + 8, file.Length);
                Assert.Equal(Header, file.AsSpan(0, 16).ToArray());
                Assert.Equal(3, BitConverter.ToInt32(file, 16));
                Assert.Equal(2, BitConverter.ToInt32(file, 20));

                // row by row, left to right, little-endian
                int position = 24;
                for (int y = 0; y < 2; ++y)
                {
                    for (int x = 0; x < 3; ++x)
                    {
                        Assert.Equal(x + 0.5f, BitConverter.ToSingle(file, position));
                        Assert.Equal(-(y * 10 + x), BitConverter.ToSingle(file, position + 4));
                        position += 8;
                    }
                }

                Assert.Equal(new byte[8], file.AsSpan(position, 8).ToArray());
            }
        }

        [Fact]
        public void The_freeze_mask_is_not_written_to_the_file()
        {
            using (DisplacementMesh masked = new DisplacementMesh(4, 4))
            using (DisplacementMesh unmasked = new DisplacementMesh(4, 4))
            {
                TestHelpers.Fill(masked, (x, y) => new DisplacementVector(x, y, 255));
                TestHelpers.Fill(unmasked, (x, y) => new DisplacementVector(x, y, 0));

                Assert.Equal(Save(unmasked), Save(masked));
            }
        }

        [Fact]
        public void A_file_loads_back_into_a_mesh_of_the_same_size()
        {
            byte[] file = BuildFile(5, 4, (x, y) => new PointF(x * 1.5f, y - 2.25f));

            using (DisplacementMesh mesh = new DisplacementMesh(5, 4))
            {
                mesh.Load(new MemoryStream(file));

                for (int y = 0; y < 4; ++y)
                {
                    for (int x = 0; x < 5; ++x)
                    {
                        Assert.Equal(x * 1.5f, mesh[x, y].X);
                        Assert.Equal(y - 2.25f, mesh[x, y].Y);
                    }
                }
            }
        }

        [Fact]
        public void Saving_and_loading_gives_back_the_same_offsets()
        {
            using (DisplacementMesh original = new DisplacementMesh(31, 17))
            using (DisplacementMesh loaded = new DisplacementMesh(31, 17))
            {
                TestHelpers.Fill(original, (x, y) => new DisplacementVector((float)Math.Sin(x) * 40, (float)Math.Cos(y * 0.3) * 25));

                loaded.Load(new MemoryStream(Save(original)));

                Assert.Null(TestHelpers.FirstDifference(original, loaded));
            }
        }

        [Fact]
        public void Loading_keeps_the_current_freeze_mask()
        {
            byte[] sameSize = BuildFile(8, 8, (x, y) => new PointF(1, 2));
            byte[] otherSize = BuildFile(16, 16, (x, y) => new PointF(1, 2));

            foreach (byte[] file in new[] { sameSize, otherSize })
            {
                using (DisplacementMesh mesh = new DisplacementMesh(8, 8))
                {
                    TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(0, 0, 123));

                    mesh.Load(new MemoryStream(file));

                    Assert.Equal(123, mesh[4, 4].Mask);
                    Assert.NotEqual(0f, mesh[4, 4].X);
                }
            }
        }

        [Fact]
        public void A_file_for_a_different_image_size_is_scaled_to_fit()
        {
            // made for an image twice as wide and three times as tall
            byte[] file = BuildFile(20, 30, (x, y) => new PointF(6, 9));

            using (DisplacementMesh mesh = new DisplacementMesh(10, 10))
            {
                mesh.Load(new MemoryStream(file));

                Assert.Equal(new Size(10, 10), mesh.Size);
                Assert.Equal(3f, mesh[5, 5].X, 3);
                Assert.Equal(3f, mesh[5, 5].Y, 3);
            }
        }

        [Fact]
        public void A_scaled_file_lines_up_with_the_image_pixel_for_pixel()
        {
            // twice the size, with an offset that grows along x: file vector fx holds fx
            byte[] file = BuildFile(20, 20, (x, y) => new PointF(x, 0));

            using (DisplacementMesh mesh = new DisplacementMesh(10, 10))
            {
                mesh.Load(new MemoryStream(file));

                // pixel 4 of the smaller image covers file pixels 8 and 9, so it gets the offset
                // between them (8.5), halved along with the image
                Assert.Equal(4.25f, mesh[4, 5].X, 3);
            }
        }

        [Fact]
        public void Something_that_is_not_a_mesh_file_is_rejected_and_leaves_the_mesh_alone()
        {
            byte[] notAMesh = new byte[200];
            new Random(1).NextBytes(notAMesh);

            using (DisplacementMesh mesh = new DisplacementMesh(6, 6))
            {
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(7, 7));

                Assert.ThrowsAny<Exception>(() => mesh.Load(new MemoryStream(notAMesh)));
                Assert.ThrowsAny<Exception>(() => mesh.Load(new MemoryStream(new byte[5])));

                Assert.Equal(7f, mesh[3, 3].X);
            }
        }
    }
}
