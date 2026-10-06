using PaintDotNet;
using pyrochild.effects.common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Xunit;

namespace pyrochild.effects.liquify.tests
{
    /// <summary>
    /// The canvas control, hosted in a real but off-screen window that never takes focus. Input is
    /// sent as window messages, and what it draws is read back with DrawToBitmap, so nothing here
    /// depends on the screen, the mouse or which window is in front.
    /// </summary>
    public class CanvasPanelTests
    {
        private const int WM_HSCROLL = 0x0114;
        private const int WM_VSCROLL = 0x0115;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_MOUSEHWHEEL = 0x020E;
        private const int MK_LBUTTON = 0x0001;

        // the space the canvas keeps around the image when it doesn't fit
        private const int Margin = 10;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private sealed class HostForm : Form
        {
            protected override bool ShowWithoutActivation
            {
                get { return true; }
            }
        }

        /// <summary>
        /// Runs a test against a canvas showing a position-coded image of the given size.
        /// </summary>
        private static void WithCanvas(int imageWidth, int imageHeight, Action<CanvasPanel> test)
        {
            WithCanvas(() => TestHelpers.PositionSurface(imageWidth, imageHeight), test);
        }

        private static void WithCanvas(Func<Surface> createSurface, Action<CanvasPanel> test)
        {
            TestHelpers.RunSta(() =>
            {
                using (Surface surface = createSurface())
                using (HostForm form = new HostForm())
                {
                    form.FormBorderStyle = FormBorderStyle.None;
                    form.ShowInTaskbar = false;
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-20000, -20000);
                    form.ClientSize = new Size(800, 600);

                    CanvasPanel canvas = new CanvasPanel();
                    canvas.Bounds = new Rectangle(0, 0, 800, 600);
                    form.Controls.Add(canvas);
                    form.Show();

                    canvas.Surface = surface;
                    Application.DoEvents();

                    test(canvas);
                }
            });
        }

        private static IntPtr Position(int x, int y)
        {
            return (IntPtr)((y << 16) | (x & 0xFFFF));
        }

        private static void Pump(int milliseconds)
        {
            Stopwatch clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < milliseconds)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(5);
            }
        }

        // where the image's top left corner is drawn, in canvas client coordinates
        private static Point ImageOrigin(CanvasPanel canvas, int imageWidth, int imageHeight)
        {
            Size client = canvas.ClientSize;
            int width = (int)(imageWidth * canvas.ZoomFactor);
            int height = (int)(imageHeight * canvas.ZoomFactor);

            int x = client.Width > width + 2 * Margin ? (client.Width - width) / 2 : Margin - canvas.ScrollPosition.X;
            int y = client.Height > height + 2 * Margin ? (client.Height - height) / 2 : Margin - canvas.ScrollPosition.Y;
            return new Point(x, y);
        }

        private static void AssertDrawsTheImage(CanvasPanel canvas, int imageWidth, int imageHeight)
        {
            Size client = canvas.ClientSize;
            Point origin = ImageOrigin(canvas, imageWidth, imageHeight);
            int width = (int)(imageWidth * canvas.ZoomFactor);
            int height = (int)(imageHeight * canvas.ZoomFactor);

            using (Bitmap drawn = new Bitmap(canvas.Width, canvas.Height))
            {
                canvas.DrawToBitmap(drawn, new Rectangle(Point.Empty, canvas.Size));

                int checkedPixels = 0;
                for (int y = 0; y < client.Height; y += 7)
                {
                    for (int x = 0; x < client.Width; x += 5)
                    {
                        int cx = x - origin.X;
                        int cy = y - origin.Y;
                        if (cx < 0 || cy < 0 || cx >= width || cy >= height)
                        {
                            continue;
                        }

                        // the image pixel under the middle of this screen pixel
                        int sourceX = (int)((2L * cx + 1) * imageWidth / (2L * width));
                        int sourceY = (int)((2L * cy + 1) * imageHeight / (2L * height));
                        Color expected = TestHelpers.PositionColor(sourceX, sourceY).ToColor();
                        Color actual = drawn.GetPixel(x, y);

                        Assert.True(expected.R == actual.R && expected.G == actual.G && expected.B == actual.B,
                            string.Format("at {0},{1} (image {2},{3}): expected {4} but drew {5}", x, y, sourceX, sourceY, expected, actual));
                        ++checkedPixels;
                    }
                }

                Assert.True(checkedPixels > 1000, "only " + checkedPixels + " pixels were over the image");
            }
        }

        [Fact]
        public unsafe void Pixels_that_are_not_a_Surface_can_be_shown_and_taken_away_again()
        {
            WithCanvas(40, 30, canvas =>
            {
                // part of a wider picture, so the stride is more than the width
                using (Surface backing = TestHelpers.PositionSurface(500, 200))
                {
                    canvas.Surface = new BitmapSurface((ColorBgra*)backing.Scan0.VoidStar, 300, 200, backing.Stride);
                    Application.DoEvents();

                    Assert.Equal(300, canvas.Surface.Width);
                    AssertDrawsTheImage(canvas, 300, 200);

                    canvas.Surface = null;
                }

                // painting must not touch the freed pixels
                using (Bitmap drawn = new Bitmap(canvas.Width, canvas.Height))
                {
                    canvas.DrawToBitmap(drawn, new Rectangle(Point.Empty, canvas.Size));
                }
            });
        }

        [Fact]
        public void The_clipboard_swatch_is_only_made_again_when_the_clipboard_changes()
        {
            TestHelpers.RunSta(() =>
            {
                using (CanvasPanel canvas = new CanvasPanel())
                {
                    uint sequence = 7;
                    int reads = 0;
                    Color color = Color.Red;

                    canvas.ClipboardSequence = () => sequence;
                    canvas.ReadClipboardImage = () =>
                    {
                        ++reads;
                        Bitmap whole = new Bitmap(200, 120);
                        using (Graphics g = Graphics.FromImage(whole))
                        {
                            g.Clear(color);
                        }
                        return whole;
                    };

                    Bitmap first = (Bitmap)canvas.GetClipboardSwatch();
                    Assert.Equal(new Size(16, 16), first.Size);
                    Assert.Equal(Color.Red.ToArgb(), first.GetPixel(8, 8).ToArgb());

                    Assert.Same(first, canvas.GetClipboardSwatch());
                    Assert.Equal(1, reads);

                    sequence = 8;
                    color = Color.Blue;
                    Bitmap second = (Bitmap)canvas.GetClipboardSwatch();
                    Assert.Equal(2, reads);
                    Assert.Equal(Color.Blue.ToArgb(), second.GetPixel(8, 8).ToArgb());
                }
            });
        }

        [Fact]
        public void A_clipboard_with_no_image_or_one_that_cannot_be_read_gives_no_swatch()
        {
            TestHelpers.RunSta(() =>
            {
                using (CanvasPanel canvas = new CanvasPanel())
                {
                    int reads = 0;
                    canvas.ClipboardSequence = () => 1;

                    // another program has the clipboard open
                    canvas.ReadClipboardImage = () =>
                    {
                        ++reads;
                        throw new System.Runtime.InteropServices.ExternalException("clipboard busy");
                    };
                    Assert.Null(canvas.GetClipboardSwatch());

                    // a failed read isn't remembered
                    Assert.Null(canvas.GetClipboardSwatch());
                    Assert.Equal(2, reads);

                    // no image; this answer is remembered
                    canvas.ReadClipboardImage = () =>
                    {
                        ++reads;
                        return null;
                    };
                    Assert.Null(canvas.GetClipboardSwatch());
                    Assert.Null(canvas.GetClipboardSwatch());
                    Assert.Equal(3, reads);
                }
            });
        }

        [Theory]
        [InlineData(1f, 0, 0)]
        [InlineData(1f, 137, 91)]
        [InlineData(2f, 400, 333)]
        [InlineData(0.5f, 0, 0)]
        [InlineData(0.66f, 20, 5)]
        [InlineData(1.5f, 250, 180)]
        [InlineData(3f, 1000, 900)]
        public void The_image_is_drawn_in_the_right_place_at_any_zoom_and_scroll(float zoom, int scrollX, int scrollY)
        {
            WithCanvas(1000, 800, canvas =>
            {
                canvas.ZoomFactor = zoom;
                canvas.ScrollPosition = new Point(scrollX, scrollY);

                AssertDrawsTheImage(canvas, 1000, 800);
            });
        }

        [Fact]
        public void Zoom_to_fit_fills_the_view_with_the_whole_image()
        {
            WithCanvas(1000, 800, canvas =>
            {
                // 800x600 less a margin holds about 72% of 1000x800, which is between two zoom levels
                canvas.ZoomToFit();
                Assert.True(canvas.ZoomedToFit);
                Assert.InRange(canvas.ZoomFactor, 0.67f, 0.75f);
                Assert.Equal(Point.Empty, canvas.ScrollPosition);

                // the whole image is in view, and one of its sides nearly fills it
                Assert.True(800 * canvas.ZoomFactor < canvas.Height);
                Assert.True(800 * canvas.ZoomFactor > canvas.Height - 60);

                // it follows the panel's size until another zoom is set
                float before = canvas.ZoomFactor;
                canvas.Size = new Size(canvas.Width, canvas.Height - 100);
                Assert.True(canvas.ZoomFactor < before);
                Assert.True(canvas.ZoomedToFit);

                canvas.ZoomFactor = 0.5f;
                Assert.False(canvas.ZoomedToFit);
                canvas.Size = new Size(canvas.Width, canvas.Height + 100);
                Assert.Equal(0.5f, canvas.ZoomFactor);
            });
        }

        [Fact]
        public void Zooming_in_or_out_from_a_fitted_zoom_goes_to_the_neighbouring_level()
        {
            WithCanvas(1000, 800, canvas =>
            {
                canvas.ZoomToFit(); // about 72%
                canvas.ZoomIn();
                Assert.Equal(1f, canvas.ZoomFactor);

                canvas.ZoomToFit();
                canvas.ZoomOut();
                Assert.Equal(0.66f, canvas.ZoomFactor);

                // and from a level, one level at a time as before
                canvas.ZoomOut();
                Assert.Equal(0.5f, canvas.ZoomFactor);
                canvas.ZoomIn();
                Assert.Equal(0.66f, canvas.ZoomFactor);
            });
        }

        [Fact]
        public void Zoom_to_fit_never_enlarges_a_small_image()
        {
            WithCanvas(120, 90, canvas =>
            {
                canvas.ZoomFactor = 0.25f;
                canvas.ZoomToFit();
                Assert.Equal(1f, canvas.ZoomFactor);
            });
        }

        [Fact]
        public void The_scroll_position_is_kept_within_the_image()
        {
            WithCanvas(1000, 800, canvas =>
            {
                canvas.ZoomFactor = 2f;

                canvas.ScrollPosition = new Point(-50, -50);
                Assert.Equal(Point.Empty, canvas.ScrollPosition);

                canvas.ScrollPosition = new Point(100000, 100000);
                Size client = canvas.ClientSize;
                Assert.Equal(new Point(2000 + 2 * Margin - client.Width, 1600 + 2 * Margin - client.Height), canvas.ScrollPosition);

                // an image that fits can't be scrolled at all
                canvas.ZoomFactor = 0.25f;
                Assert.Equal(Point.Empty, canvas.ScrollPosition);
                canvas.ScrollPosition = new Point(30, 30);
                Assert.Equal(Point.Empty, canvas.ScrollPosition);
            });
        }

        [Fact]
        public void Scrollbars_take_space_only_when_the_image_does_not_fit()
        {
            WithCanvas(1000, 800, canvas =>
            {
                canvas.ZoomFactor = 0.5f;
                Assert.Equal(canvas.Size, canvas.ClientSize);

                canvas.ZoomFactor = 2f;
                Assert.True(canvas.ClientSize.Width < canvas.Width, "expected a vertical scrollbar");
                Assert.True(canvas.ClientSize.Height < canvas.Height, "expected a horizontal scrollbar");

                canvas.ZoomFactor = 0.5f;
                Assert.Equal(canvas.Size, canvas.ClientSize);
            });
        }

        [Theory]
        [InlineData(1f, 0, 0)]
        [InlineData(2f, 300, 200)]
        [InlineData(0.5f, 0, 0)]
        public void Mouse_events_are_reported_in_image_coordinates(float zoom, int scrollX, int scrollY)
        {
            WithCanvas(1000, 800, canvas =>
            {
                canvas.ZoomFactor = zoom;
                canvas.ScrollPosition = new Point(scrollX, scrollY);
                Point origin = ImageOrigin(canvas, 1000, 800);

                List<CanvasMouseEventArgs> downs = new List<CanvasMouseEventArgs>();
                List<CanvasMouseEventArgs> moves = new List<CanvasMouseEventArgs>();
                canvas.CanvasMouseDown += (s, e) => downs.Add(e);
                canvas.CanvasMouseMove += (s, e) => moves.Add(e);

                SendMessage(canvas.Handle, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, Position(300, 250));
                SendMessage(canvas.Handle, WM_MOUSEMOVE, (IntPtr)MK_LBUTTON, Position(340, 280));

                Assert.Single(downs);
                Assert.Equal(MouseButtons.Left, downs[0].Button);
                Assert.Equal((300 - origin.X) / zoom, downs[0].X, 2);
                Assert.Equal((250 - origin.Y) / zoom, downs[0].Y, 2);

                Assert.NotEmpty(moves);
                CanvasMouseEventArgs last = moves[moves.Count - 1];
                Assert.Equal(MouseButtons.Left, last.Button);
                Assert.Equal((340 - origin.X) / zoom, last.X, 2);
                Assert.Equal((280 - origin.Y) / zoom, last.Y, 2);

                SendMessage(canvas.Handle, WM_LBUTTONUP, IntPtr.Zero, Position(340, 280));
                Pump(150);
            });
        }

        [Fact]
        public void A_held_button_reported_as_release_and_press_pairs_is_one_drag()
        {
            // what a laptop with a touchpad and a pointing stick can send when the button is held on
            // one and the movement comes from the other
            WithCanvas(1000, 800, canvas =>
            {
                int downs = 0, ups = 0, movesWithButton = 0, movesWithout = 0;
                canvas.CanvasMouseDown += (s, e) => ++downs;
                canvas.CanvasMouseUp += (s, e) => ++ups;
                canvas.CanvasMouseMove += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left) ++movesWithButton; else ++movesWithout;
                };

                SendMessage(canvas.Handle, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, Position(200, 200));
                for (int i = 1; i <= 12; ++i)
                {
                    int x = 200 + i * 6;

                    // moves from the second device carry no button
                    SendMessage(canvas.Handle, WM_MOUSEMOVE, IntPtr.Zero, Position(x, 200));

                    if (i % 3 == 0)
                    {
                        SendMessage(canvas.Handle, WM_LBUTTONUP, IntPtr.Zero, Position(x, 200));
                        SendMessage(canvas.Handle, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, Position(x, 200));
                    }
                }

                Assert.Equal(1, downs);
                Assert.Equal(0, ups);
                Assert.Equal(12, movesWithButton);
                Assert.Equal(0, movesWithout);

                // the real release ends it, a moment later
                SendMessage(canvas.Handle, WM_LBUTTONUP, IntPtr.Zero, Position(280, 200));
                Assert.Equal(0, ups);
                Pump(250);
                Assert.Equal(1, downs);
                Assert.Equal(1, ups);

                // and after that a move is just a move
                SendMessage(canvas.Handle, WM_MOUSEMOVE, IntPtr.Zero, Position(300, 220));
                Assert.Equal(1, movesWithout);
            });
        }

        [Fact]
        public void Two_separate_clicks_stay_separate()
        {
            WithCanvas(1000, 800, canvas =>
            {
                int downs = 0, ups = 0;
                canvas.CanvasMouseDown += (s, e) => ++downs;
                canvas.CanvasMouseUp += (s, e) => ++ups;

                for (int click = 0; click < 2; ++click)
                {
                    SendMessage(canvas.Handle, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, Position(200, 200));
                    SendMessage(canvas.Handle, WM_LBUTTONUP, IntPtr.Zero, Position(200, 200));
                    Pump(200);
                }

                Assert.Equal(2, downs);
                Assert.Equal(2, ups);
            });
        }

        [Fact]
        public void Wheel_and_scrollbar_messages_scroll_on_the_right_axis()
        {
            WithCanvas(1000, 800, canvas =>
            {
                canvas.ZoomFactor = 3f;
                canvas.ScrollPosition = new Point(500, 500);

                // a sideways wheel or two-finger swipe: 60 to the right
                SendMessage(canvas.Handle, WM_MOUSEHWHEEL, (IntPtr)(60 << 16), IntPtr.Zero);
                Assert.Equal(new Point(560, 500), canvas.ScrollPosition);

                // the vertical wheel, as the owner passes it on: down is negative
                canvas.PerformMouseWheel(new MouseEventArgs(MouseButtons.None, 0, 0, 0, -120));
                Assert.Equal(new Point(560, 620), canvas.ScrollPosition);

                // what a touchpad driver that scrolls through the scrollbars sends: one line right, one line up
                Point before = canvas.ScrollPosition;
                SendMessage(canvas.Handle, WM_HSCROLL, (IntPtr)1, IntPtr.Zero);
                Assert.True(canvas.ScrollPosition.X > before.X && canvas.ScrollPosition.Y == before.Y, "line right: " + canvas.ScrollPosition);

                before = canvas.ScrollPosition;
                SendMessage(canvas.Handle, WM_VSCROLL, (IntPtr)0, IntPtr.Zero);
                Assert.True(canvas.ScrollPosition.Y < before.Y && canvas.ScrollPosition.X == before.X, "line up: " + canvas.ScrollPosition);

                // jump to a position, the position being the high word
                SendMessage(canvas.Handle, WM_HSCROLL, (IntPtr)((1234 << 16) | 4), IntPtr.Zero);
                Assert.Equal(1234, canvas.ScrollPosition.X);
            });
        }

        // a 400x300 image: transparent on the left, half transparent in the middle, opaque on the right
        private static Surface PartlyTransparentSurface()
        {
            Surface surface = new Surface(400, 300);
            for (int y = 0; y < 300; ++y)
            {
                for (int x = 0; x < 400; ++x)
                {
                    byte alpha = x < 150 ? (byte)0 : x < 250 ? (byte)128 : (byte)255;
                    surface[x, y] = ColorBgra.FromBgra(0, 0, 200, alpha);
                }
            }
            return surface;
        }

        private static Color DrawnAt(CanvasPanel canvas, int imageX, int imageY)
        {
            Point origin = ImageOrigin(canvas, canvas.Surface.Width, canvas.Surface.Height);
            using (Bitmap drawn = new Bitmap(canvas.Width, canvas.Height))
            {
                canvas.DrawToBitmap(drawn, new Rectangle(Point.Empty, canvas.Size));
                return drawn.GetPixel(origin.X + imageX, origin.Y + imageY);
            }
        }

        [Fact]
        public void A_background_surface_shows_through_where_the_image_is_transparent()
        {
            WithCanvas(PartlyTransparentSurface, canvas =>
            {
                canvas.ZoomFactor = 1f;

                using (Surface background = new Surface(400, 300))
                {
                    background.Fill(ColorBgra.FromBgra(40, 220, 20, 255)); // opaque green

                    // without it, transparent parts show the checkerboard, which is white or light gray
                    Color before = DrawnAt(canvas, 50, 50);
                    Assert.True(before.R == before.G && before.G == before.B && before.R >= 191, "expected checkerboard, drew " + before);

                    canvas.BackgroundSurface = background;

                    Color transparentPart = DrawnAt(canvas, 50, 50);
                    Assert.Equal(Color.FromArgb(20, 220, 40).ToArgb(), Color.FromArgb(transparentPart.R, transparentPart.G, transparentPart.B).ToArgb());

                    // half-transparent red over the green: about half of each
                    Color mixed = DrawnAt(canvas, 200, 50);
                    Assert.InRange(mixed.R, 105, 115);
                    Assert.InRange(mixed.G, 105, 115);

                    // the opaque part is the image alone
                    Color opaquePart = DrawnAt(canvas, 350, 50);
                    Assert.Equal(Color.FromArgb(200, 0, 0).ToArgb(), Color.FromArgb(opaquePart.R, opaquePart.G, opaquePart.B).ToArgb());

                    canvas.BackgroundSurface = null;
                    Color after = DrawnAt(canvas, 50, 50);
                    Assert.Equal(before.ToArgb(), after.ToArgb());
                }
            });
        }

        private static Bitmap SolidPicture(int width, int height, Color color)
        {
            Bitmap picture = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(picture))
            {
                g.Clear(color);
            }
            return picture;
        }

        private static bool IsDisposed(Image image)
        {
            try
            {
                return image.Width < 0;
            }
            catch (ArgumentException)
            {
                return true; // what GDI+ throws for a disposed image
            }
        }

        [Fact]
        public void A_picture_as_the_background_shows_through_and_keeps_the_overlay_and_the_layers_in_front()
        {
            WithCanvas(PartlyTransparentSurface, canvas =>
            {
                canvas.ZoomFactor = 1f;

                // not the image's size: it gets stretched
                Bitmap green = SolidPicture(64, 48, Color.FromArgb(20, 220, 40));
                canvas.SetBackgroundImage(green);

                Color transparentPart = DrawnAt(canvas, 50, 50);
                Assert.Equal(Color.FromArgb(20, 220, 40).ToArgb(), Color.FromArgb(transparentPart.R, transparentPart.G, transparentPart.B).ToArgb());

                Color opaquePart = DrawnAt(canvas, 350, 50);
                Assert.Equal(Color.FromArgb(200, 0, 0).ToArgb(), Color.FromArgb(opaquePart.R, opaquePart.G, opaquePart.B).ToArgb());

                // the row overlay still runs
                int overlayRows = 0;
                canvas.RowOverlay = (pixels, x, y, count, scale) => System.Threading.Interlocked.Increment(ref overlayRows);
                DrawnAt(canvas, 50, 50);
                Assert.True(overlayRows > 0, "the overlay was not drawn");
                canvas.RowOverlay = null;

                // and so does a layer in front
                using (Surface blue = new Surface(400, 300))
                {
                    blue.Fill(ColorBgra.FromBgra(255, 0, 0, 255));
                    canvas.ForegroundLayers = new[] { new CanvasForegroundLayer(blue) };

                    Color covered = DrawnAt(canvas, 50, 50);
                    Assert.Equal(Color.FromArgb(0, 0, 255).ToArgb(), Color.FromArgb(covered.R, covered.G, covered.B).ToArgb());

                    canvas.ForegroundLayers = null;
                }

                // BackgroundSurfaceHidden hides it too
                canvas.BackgroundSurfaceHidden = true;
                Color hidden = DrawnAt(canvas, 50, 50);
                Assert.True(hidden.R == hidden.G && hidden.G == hidden.B && hidden.R >= 191, "expected checkerboard, drew " + hidden);
                canvas.BackgroundSurfaceHidden = false;

                // replacing or removing it disposes the old picture
                Bitmap yellow = SolidPicture(10, 10, Color.Yellow);
                canvas.SetBackgroundImage(yellow);
                Assert.True(IsDisposed(green));

                Color replaced = DrawnAt(canvas, 50, 50);
                Assert.Equal(Color.Yellow.ToArgb(), Color.FromArgb(replaced.R, replaced.G, replaced.B).ToArgb());

                canvas.SetBackgroundImage(null);
                Assert.True(IsDisposed(yellow));

                Color after = DrawnAt(canvas, 50, 50);
                Assert.True(after.R == after.G && after.G == after.B && after.R >= 191, "expected checkerboard, drew " + after);
            });
        }

        [Fact]
        public void The_owner_can_end_a_drag_without_waiting_for_the_button()
        {
            WithCanvas(1000, 800, canvas =>
            {
                int downs = 0, ups = 0;
                canvas.CanvasMouseDown += (s, e) => ++downs;
                canvas.CanvasMouseUp += (s, e) => ++ups;

                SendMessage(canvas.Handle, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, Position(200, 200));
                SendMessage(canvas.Handle, WM_MOUSEMOVE, (IntPtr)MK_LBUTTON, Position(220, 210));
                Assert.Equal(1, downs);
                Assert.Equal(0, ups);

                canvas.EndMouseDrag();
                Pump(200);
                Assert.Equal(1, ups);

                // with nothing being dragged it does nothing
                canvas.EndMouseDrag();
                Pump(200);
                Assert.Equal(1, ups);
            });
        }

        [Fact]
        public void The_background_choices_can_be_put_on_a_menu_of_the_owners()
        {
            WithCanvas(PartlyTransparentSurface, canvas =>
            {
                using (ContextMenuStrip menu = new ContextMenuStrip())
                {
                    menu.Items.Add("left over from last time");
                    canvas.BackColor = Color.White;

                    // the area around the canvas: plain colors only
                    canvas.FillBackgroundMenu(menu, false);

                    string[] names = new string[menu.Items.Count];
                    for (int i = 0; i < names.Length; ++i)
                    {
                        names[i] = menu.Items[i].Text;
                    }
                    Assert.Equal(new[] { "Black", "White", "Gray", "Other color..." }, names);

                    menu.Items[0].PerformClick();
                    Assert.Equal(Color.Black.ToArgb(), canvas.BackColor.ToArgb());
                }
            });
        }

        [Fact]
        public void A_foreground_surface_is_drawn_over_the_image()
        {
            WithCanvas(PartlyTransparentSurface, canvas =>
            {
                canvas.ZoomFactor = 1f;

                using (Surface foreground = new Surface(400, 300))
                {
                    // opaque blue on the top half, nothing on the bottom half
                    foreground.Fill(ColorBgra.FromBgra(0, 0, 0, 0));
                    foreground.Fill(new Rectangle(0, 0, 400, 150), ColorBgra.FromBgra(230, 30, 10, 255));

                    Color opaqueBefore = DrawnAt(canvas, 350, 250);
                    Color clearBefore = DrawnAt(canvas, 50, 250);

                    canvas.ForegroundLayers = new[] { new CanvasForegroundLayer(foreground) };

                    // it covers the image and the checkerboard alike
                    foreach (int x in new[] { 50, 200, 350 })
                    {
                        Color covered = DrawnAt(canvas, x, 50);
                        Assert.Equal(Color.FromArgb(10, 30, 230).ToArgb(), Color.FromArgb(covered.R, covered.G, covered.B).ToArgb());
                    }

                    // and leaves everything alone where it is transparent
                    Assert.Equal(opaqueBefore.ToArgb(), DrawnAt(canvas, 350, 250).ToArgb());
                    Assert.Equal(clearBefore.ToArgb(), DrawnAt(canvas, 50, 250).ToArgb());

                    canvas.ForegroundLayers = null;
                    Assert.Equal(opaqueBefore.ToArgb(), DrawnAt(canvas, 350, 50).ToArgb());
                }
            });
        }

        [Fact]
        public void A_foreground_layer_with_a_blend_mode_is_blended_onto_what_is_under_it()
        {
            WithCanvas(PartlyTransparentSurface, canvas =>
            {
                canvas.ZoomFactor = 1f;

                using (Surface gray = new Surface(400, 300))
                using (Surface black = new Surface(400, 300))
                {
                    gray.Fill(ColorBgra.FromBgra(128, 128, 128, 255));
                    black.Fill(ColorBgra.FromBgra(0, 0, 0, 255));

                    // the opaque part of the image is red 200
                    Color before = DrawnAt(canvas, 350, 50);
                    Assert.Equal(200, before.R);

                    // multiplying by mid gray halves it, where a plain overlay would have covered it
                    canvas.ForegroundLayers = new[]
                    {
                        new CanvasForegroundLayer(gray, PaintDotNet.LayerBlendModeUtil.CreateCompositionOp(LayerBlendMode.Multiply, 255))
                    };
                    Color multiplied = DrawnAt(canvas, 350, 50);
                    Assert.InRange(multiplied.R, 98, 102);
                    Assert.Equal(0, multiplied.G);

                    // layers apply bottom first: black laid over that hides it
                    canvas.ForegroundLayers = new[]
                    {
                        new CanvasForegroundLayer(gray, PaintDotNet.LayerBlendModeUtil.CreateCompositionOp(LayerBlendMode.Multiply, 255)),
                        new CanvasForegroundLayer(black)
                    };
                    Color covered = DrawnAt(canvas, 350, 50);
                    Assert.Equal(0, covered.R);

                    // hiding the background surface doesn't touch the choice of background
                    canvas.BackgroundSurface = gray;
                    canvas.ForegroundLayers = null;
                    canvas.BackgroundSurfaceHidden = true;
                    Color hidden = DrawnAt(canvas, 50, 50);
                    Assert.True(hidden.R >= 191, "expected checkerboard, drew " + hidden);
                    Assert.Same(gray, canvas.BackgroundSurface);
                    canvas.BackgroundSurfaceHidden = false;
                    Assert.Equal(128, DrawnAt(canvas, 50, 50).R);
                    canvas.BackgroundSurface = null;
                }
            });
        }

        [Fact]
        public void The_owner_is_told_when_the_background_surface_changes()
        {
            WithCanvas(PartlyTransparentSurface, canvas =>
            {
                int changes = 0;
                canvas.BackgroundSurfaceChanged += (s, e) => ++changes;

                using (Surface first = new Surface(400, 300))
                using (Surface second = new Surface(400, 300))
                {
                    canvas.BackgroundSurface = first;
                    Assert.Equal(1, changes);

                    // setting the same one again is not a change
                    canvas.BackgroundSurface = first;
                    Assert.Equal(1, changes);

                    canvas.BackgroundSurface = second;
                    Assert.Equal(2, changes);

                    // which is also what picking a color from the menu does
                    canvas.BackgroundSurface = null;
                    Assert.Equal(3, changes);
                    Assert.Null(canvas.BackgroundSurface);
                }
            });
        }

        [Fact]
        public void Selecting_a_background_option_shows_its_surface_and_marks_it_active()
        {
            WithCanvas(PartlyTransparentSurface, canvas =>
            {
                canvas.ZoomFactor = 1f;

                using (Surface green = new Surface(400, 300))
                using (Surface blue = new Surface(400, 300))
                {
                    green.Fill(ColorBgra.FromBgra(0, 255, 0, 255));
                    blue.Fill(ColorBgra.FromBgra(255, 0, 0, 255));

                    int greenBuilt = 0;
                    CanvasBackgroundOption greenOption = new CanvasBackgroundOption("Green", () => { ++greenBuilt; return green; });
                    CanvasBackgroundOption blueOption = new CanvasBackgroundOption("Blue", () => blue);
                    CanvasBackgroundOption missing = CanvasBackgroundOption.Unavailable("Not there");
                    canvas.BackgroundOptions.Add(greenOption);
                    canvas.BackgroundOptions.Add(blueOption);
                    canvas.BackgroundOptions.Add(missing);

                    // nothing is built until something is picked
                    Assert.Null(canvas.ActiveBackgroundOption);
                    Assert.Equal(0, greenBuilt);

                    canvas.SelectBackgroundOption(greenOption);
                    Assert.Same(greenOption, canvas.ActiveBackgroundOption);
                    Assert.Same(green, canvas.BackgroundSurface);
                    Assert.Equal(255, DrawnAt(canvas, 50, 50).G);

                    canvas.SelectBackgroundOption(blueOption);
                    Assert.Same(blueOption, canvas.ActiveBackgroundOption);
                    Assert.Equal(255, DrawnAt(canvas, 50, 50).B);

                    // an unavailable entry can't be picked, and picking nothing changes nothing
                    Assert.False(missing.Enabled);
                    canvas.SelectBackgroundOption(missing);
                    canvas.SelectBackgroundOption(null);
                    Assert.Same(blueOption, canvas.ActiveBackgroundOption);

                    // replacing the background some other way leaves no option active
                    canvas.BackgroundSurface = null;
                    Assert.Null(canvas.ActiveBackgroundOption);
                }
            });
        }

        [Fact]
        public void A_transparent_background_surface_still_shows_the_checkerboard()
        {
            WithCanvas(PartlyTransparentSurface, canvas =>
            {
                canvas.ZoomFactor = 1f;
                Color checkerboard = DrawnAt(canvas, 50, 50);

                using (Surface background = new Surface(400, 300))
                {
                    background.Fill(ColorBgra.FromBgra(0, 0, 0, 0));
                    canvas.BackgroundSurface = background;

                    Assert.Equal(checkerboard.ToArgb(), DrawnAt(canvas, 50, 50).ToArgb());
                }
            });
        }

        [Fact]
        public void A_background_surface_of_the_wrong_size_is_ignored()
        {
            WithCanvas(PartlyTransparentSurface, canvas =>
            {
                canvas.ZoomFactor = 1f;
                Color checkerboard = DrawnAt(canvas, 50, 50);

                using (Surface background = new Surface(100, 100))
                {
                    background.Fill(ColorBgra.FromBgra(40, 220, 20, 255));
                    canvas.BackgroundSurface = background;

                    Assert.Equal(checkerboard.ToArgb(), DrawnAt(canvas, 50, 50).ToArgb());
                }
            });
        }

        [Fact]
        public unsafe void The_row_overlay_is_asked_to_draw_every_row_of_the_image()
        {
            WithCanvas(400, 300, canvas =>
            {
                canvas.ZoomFactor = 1f;

                object sync = new object();
                HashSet<int> rows = new HashSet<int>();
                int widest = 0;
                canvas.RowOverlay = (pixels, x, y, count, scale) =>
                {
                    lock (sync)
                    {
                        rows.Add(y);
                        widest = Math.Max(widest, x + count);
                    }

                    // paint the row a color the image doesn't contain
                    uint* p = (uint*)pixels;
                    for (int i = 0; i < count; ++i)
                    {
                        p[i] = 0xFFFF00FF;
                    }
                };

                using (Bitmap drawn = new Bitmap(canvas.Width, canvas.Height))
                {
                    canvas.DrawToBitmap(drawn, new Rectangle(Point.Empty, canvas.Size));

                    Assert.Equal(300, rows.Count);
                    Assert.Equal(400, widest);

                    Point origin = ImageOrigin(canvas, 400, 300);
                    Color inside = drawn.GetPixel(origin.X + 200, origin.Y + 150);
                    Assert.True(inside.R == 255 && inside.G == 0 && inside.B == 255, "the overlay's pixels should be what is drawn, was " + inside);
                }
            });
        }
    }
}
