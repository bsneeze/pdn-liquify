using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace pyrochild.effects.liquify
{
    /// <summary>
    /// This struct is a vector giving the offset to the source pixel to use as the dest pixel:
    /// dest.Point = source.Point + DisplacementVector
    /// Mask is used for freezing and thawing points in the mesh
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct DisplacementVector
    {
        [FieldOffset(0)]
        public float X;

        [FieldOffset(4)]
        public float Y;

        [FieldOffset(8)]
        public byte Mask;

        public DisplacementVector(float x, float y)
        {
            X = x;
            Y = y;
            Mask = 0;
        }

        public DisplacementVector(float x, float y, byte mask)
        {
            X = x;
            Y = y;
            Mask = mask;
        }

        public int SizeOf
        {
            get { return 9; }
        }

        public static DisplacementVector Zero
        {
            get
            {
                return new DisplacementVector();
            }
        }

        /// <summary>
        /// linear interpolation
        /// </summary>
        /// <param name="lhs">vector 1 (left hand side)</param>
        /// <param name="rhs">vector 2 (right hand side)</param>
        /// <param name="factor">value between 0 (lhs) and 1 (rhs).</param>
        /// <returns>interpolation between lhs and rhs, weighted by factor</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DisplacementVector Lerp(DisplacementVector lhs, DisplacementVector rhs, float factor)
        {
            float invfactor = 1 - factor;
            return new DisplacementVector(
                lhs.X * invfactor + rhs.X * factor,
                lhs.Y * invfactor + rhs.Y * factor);
        }
    }
}