using PaintDotNet;
using Xunit;

namespace pyrochild.effects.liquify.tests
{
    /// <summary>
    /// Blending one layer onto what is under it, which is what the "layer beneath" backgrounds are
    /// built from. Reading the layers themselves needs Paint.NET running and isn't covered here.
    /// </summary>
    public class LayerCompositorTests
    {
        private static Surface Filled(ColorBgra color)
        {
            Surface surface = new Surface(8, 6);
            surface.Fill(color);
            return surface;
        }

        // Properties, not fields: a field of a Paint.NET type makes the runtime load that type while
        // the tests are still being discovered, before PaintDotNetAssemblies has said where to find it,
        // and then no tests are found at all.
        private static ColorBgra Transparent { get { return ColorBgra.FromBgra(0, 0, 0, 0); } }
        private static ColorBgra Red { get { return ColorBgra.FromBgra(0, 0, 255, 255); } }
        private static ColorBgra Blue { get { return ColorBgra.FromBgra(255, 0, 0, 255); } }

        [Fact]
        public void An_opaque_layer_at_full_opacity_replaces_what_is_under_it()
        {
            using (Surface destination = Filled(Blue))
            using (Surface layer = Filled(Red))
            {
                LayerCompositor.BlendLayer(destination, layer, LayerBlendMode.Normal, 1f);

                Assert.True(destination[3, 3] == Red);
                Assert.True(destination[7, 5] == Red);
            }
        }

        [Fact]
        public void A_layer_over_nothing_keeps_its_color_and_takes_the_layer_opacity()
        {
            using (Surface destination = Filled(Transparent))
            using (Surface layer = Filled(Red))
            {
                LayerCompositor.BlendLayer(destination, layer, LayerBlendMode.Normal, 0.5f);

                Assert.Equal(255, destination[3, 3].R);
                Assert.Equal(0, destination[3, 3].B);
                Assert.InRange(destination[3, 3].A, 126, 129);
            }
        }

        [Fact]
        public void A_half_opaque_layer_mixes_with_what_is_under_it()
        {
            using (Surface destination = Filled(Blue))
            using (Surface layer = Filled(Red))
            {
                LayerCompositor.BlendLayer(destination, layer, LayerBlendMode.Normal, 0.5f);

                Assert.InRange(destination[3, 3].R, 125, 130);
                Assert.InRange(destination[3, 3].B, 125, 130);
                Assert.Equal(255, destination[3, 3].A);
            }
        }

        [Fact]
        public void A_transparent_or_fully_faded_layer_changes_nothing()
        {
            using (Surface destination = Filled(Blue))
            using (Surface clear = Filled(Transparent))
            using (Surface red = Filled(Red))
            {
                LayerCompositor.BlendLayer(destination, clear, LayerBlendMode.Normal, 1f);
                Assert.True(destination[3, 3] == Blue);

                LayerCompositor.BlendLayer(destination, red, LayerBlendMode.Normal, 0f);
                Assert.True(destination[3, 3] == Blue);
            }
        }

        [Fact]
        public void The_menu_preview_is_a_small_picture_of_the_surface()
        {
            using (Surface surface = new Surface(320, 200))
            {
                // red on the left half, transparent on the right
                for (int y = 0; y < 200; ++y)
                {
                    for (int x = 0; x < 320; ++x)
                    {
                        surface[x, y] = x < 160 ? Red : Transparent;
                    }
                }

                using (System.Drawing.Bitmap preview = LayerCompositor.CreatePreview(surface))
                {
                    Assert.Equal(new System.Drawing.Size(16, 16), preview.Size);

                    System.Drawing.Color left = preview.GetPixel(3, 8);
                    Assert.True(left.R == 255 && left.G == 0 && left.B == 0 && left.A == 255, "left was " + left);
                    Assert.Equal(0, preview.GetPixel(12, 8).A);
                }

                // the preview is a copy: it must outlive the surface it was made from
            }
        }

        [Fact]
        public void Blending_one_pixel_gives_the_same_answer_as_blending_a_whole_layer()
        {
            ColorBgra under = ColorBgra.FromBgra(200, 40, 90, 255);
            ColorBgra over = ColorBgra.FromBgra(10, 250, 60, 180);

            foreach (LayerBlendMode mode in new[] { LayerBlendMode.Normal, LayerBlendMode.Multiply, LayerBlendMode.Screen, LayerBlendMode.Overlay })
            {
                using (Surface destination = Filled(under))
                using (Surface layer = Filled(over))
                {
                    LayerCompositor.BlendLayer(destination, layer, mode, 0.7f);

                    Assert.True(destination[2, 2] == LayerCompositor.BlendPixel(under, over, mode, 0.7f), "blend mode " + mode);
                }
            }
        }

        [Fact]
        public void Preview_samples_are_spread_evenly_and_stay_inside_the_layer()
        {
            // a 16-cell preview of a 1600-wide layer samples the middle of each 100-pixel band
            Assert.Equal(50, LayerCompositor.SampleCenter(0, 1600));
            Assert.Equal(150, LayerCompositor.SampleCenter(1, 1600));
            Assert.Equal(1550, LayerCompositor.SampleCenter(15, 1600));

            // and never falls outside, even for layers smaller than the preview
            foreach (int length in new[] { 1, 2, 7, 16, 17, 1000, 30000 })
            {
                for (int cell = 0; cell < LayerCompositor.PreviewSize; ++cell)
                {
                    Assert.InRange(LayerCompositor.SampleCenter(cell, length), 0, length - 1);
                }
            }
        }

        [Fact]
        public void The_background_choice_travels_with_the_other_dialog_settings()
        {
            ConfigToken token = new ConfigToken();
            Assert.Equal(CanvasBackground.Color, token.background);
            Assert.Equal(0, token.backgroundColor);

            token.background = CanvasBackground.AllLayersBeneath;
            token.backgroundColor = unchecked((int)0xFF102030);

            ConfigToken copy = (ConfigToken)token.Clone();
            Assert.Equal(CanvasBackground.AllLayersBeneath, copy.background);
            Assert.Equal(unchecked((int)0xFF102030), copy.backgroundColor);
        }

        [Fact]
        public void The_layer_blend_mode_is_applied()
        {
            ColorBgra gray = ColorBgra.FromBgra(128, 128, 128, 255);
            ColorBgra white = ColorBgra.FromBgra(255, 255, 255, 255);
            ColorBgra black = ColorBgra.FromBgra(0, 0, 0, 255);

            using (Surface destination = Filled(gray))
            using (Surface whiteLayer = Filled(white))
            using (Surface blackLayer = Filled(black))
            {
                // multiplying by white changes nothing; a normal blend would have turned it white
                LayerCompositor.BlendLayer(destination, whiteLayer, LayerBlendMode.Multiply, 1f);
                Assert.InRange(destination[3, 3].R, 127, 129);

                // multiplying by black gives black
                LayerCompositor.BlendLayer(destination, blackLayer, LayerBlendMode.Multiply, 1f);
                Assert.Equal(0, destination[3, 3].R);
            }
        }
    }
}
