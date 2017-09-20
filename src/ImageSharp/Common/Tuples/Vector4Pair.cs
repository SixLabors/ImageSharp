using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Common.Tuples
{
    /// <summary>
    /// Its faster to process multiple Vector4-s together, so let's pair them!
    /// On AVX2 this pair should be convertible to <see cref="Vector{T}"/> of <see cref="float"/>!
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Vector4Pair
    {
        public Vector4 A;

        public Vector4 B;

        private static readonly Vector4 Scale = new Vector4(1 / 255f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MultiplyInplace(float value)
        {
            this.A *= value;
            this.B *= value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInplace(Vector4 value)
        {
            this.A += value;
            this.B += value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInplace(ref Vector4Pair other)
        {
            this.A += other.A;
            this.B += other.B;
        }

        /// <summary>
        /// Downscale method, specific to Jpeg color conversion. Works only if Vector{float}.Count == 4!
        /// TODO: Move it somewhere else.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RoundAndDownscalePreAvx2()
        {
            ref Vector<float> a = ref Unsafe.As<Vector4, Vector<float>>(ref this.A);
            a = a.FastRound();

            ref Vector<float> b = ref Unsafe.As<Vector4, Vector<float>>(ref this.B);
            b = b.FastRound();

            // Downscale by 1/255
            this.A *= Scale;
            this.B *= Scale;
        }

        /// <summary>
        /// AVX2-only Downscale method, specific to Jpeg color conversion.
        /// TODO: Move it somewhere else.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RoundAndDownscaleAvx2()
        {
            ref Vector<float> self = ref Unsafe.As<Vector4Pair, Vector<float>>(ref this);
            Vector<float> v = self;
            v = v.FastRound();

            // Downscale by 1/255
            v *= new Vector<float>(1 / 255f);
            self = v;
        }

        public override string ToString()
        {
            return $"{this.A}, {this.B}";
        }
    }
}