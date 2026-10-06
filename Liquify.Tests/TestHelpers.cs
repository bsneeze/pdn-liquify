using PaintDotNet;
using System;
using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace pyrochild.effects.liquify.tests
{
    internal static class TestHelpers
    {
        /// <summary>
        /// A surface where every pixel's color says where it is: R = x % 256, G = y % 256,
        /// B = x / 256 + 16 * (y / 256). Opaque.
        /// </summary>
        public static Surface PositionSurface(int width, int height)
        {
            Surface surface = new Surface(width, height);
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    surface[x, y] = PositionColor(x, y);
                }
            }
            return surface;
        }

        public static ColorBgra PositionColor(int x, int y)
        {
            return ColorBgra.FromBgra((byte)(x / 256 + 16 * (y / 256)), (byte)(y % 256), (byte)(x % 256), 255);
        }

        public static unsafe void Set(DisplacementMesh mesh, int x, int y, float dx, float dy, byte mask = 0)
        {
            DisplacementVector* p = mesh.GetPointAddressUnchecked(x, y);
            p->X = dx;
            p->Y = dy;
            p->Mask = mask;
        }

        public static void Fill(DisplacementMesh mesh, Func<int, int, DisplacementVector> value)
        {
            for (int y = 0; y < mesh.Height; ++y)
            {
                for (int x = 0; x < mesh.Width; ++x)
                {
                    DisplacementVector v = value(x, y);
                    Set(mesh, x, y, v.X, v.Y, v.Mask);
                }
            }
        }

        /// <summary>
        /// The first place two meshes differ, or null if they are the same.
        /// </summary>
        public static string FirstDifference(DisplacementMesh expected, DisplacementMesh actual)
        {
            if (expected.Size != actual.Size)
            {
                return "sizes differ: " + expected.Size + " and " + actual.Size;
            }

            for (int y = 0; y < expected.Height; ++y)
            {
                for (int x = 0; x < expected.Width; ++x)
                {
                    DisplacementVector e = expected[x, y];
                    DisplacementVector a = actual[x, y];
                    if (e.X != a.X || e.Y != a.Y || e.Mask != a.Mask)
                    {
                        return string.Format("at ({0},{1}): expected ({2}, {3}, mask {4}) but was ({5}, {6}, mask {7})",
                            x, y, e.X, e.Y, e.Mask, a.X, a.Y, a.Mask);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Runs something on a single-threaded-apartment thread, which WinForms controls need, and
        /// rethrows whatever it throws.
        /// </summary>
        public static void RunSta(Action action)
        {
            ExceptionDispatchInfo failure = null;

            Thread thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ExceptionDispatchInfo.Capture(ex);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                failure.Throw();
            }
        }
    }
}
