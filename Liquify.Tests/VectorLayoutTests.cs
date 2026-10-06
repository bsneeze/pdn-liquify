using System.Runtime.InteropServices;
using Xunit;

namespace pyrochild.effects.liquify.tests
{
    public class VectorLayoutTests
    {
        // The mesh holds one of these per pixel, so its size is most of the plugin's memory. Left to
        // itself the runtime pads the 9 bytes of fields to 12 to keep the floats aligned.
        [Fact]
        public unsafe void A_mesh_vector_takes_nine_bytes_with_no_padding()
        {
            Assert.Equal(9, sizeof(DisplacementVector));
            Assert.Equal(9, Marshal.SizeOf(typeof(DisplacementVector)));
            Assert.Equal(9, new DisplacementVector().SizeOf);

            using (DisplacementMesh mesh = new DisplacementMesh(100, 7))
            {
                Assert.Equal(900, mesh.Stride);
            }
        }

        [Fact]
        public void Packed_vectors_next_to_each_other_keep_their_own_values()
        {
            using (DisplacementMesh mesh = new DisplacementMesh(5, 3))
            {
                TestHelpers.Fill(mesh, (x, y) => new DisplacementVector(x + 0.25f, -y - 0.5f, (byte)(200 + x + y * 5)));

                for (int y = 0; y < 3; ++y)
                {
                    for (int x = 0; x < 5; ++x)
                    {
                        Assert.Equal(x + 0.25f, mesh[x, y].X);
                        Assert.Equal(-y - 0.5f, mesh[x, y].Y);
                        Assert.Equal((byte)(200 + x + y * 5), mesh[x, y].Mask);
                    }
                }
            }
        }
    }
}
