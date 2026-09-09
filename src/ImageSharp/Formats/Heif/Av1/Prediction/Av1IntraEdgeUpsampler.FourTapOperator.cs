// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal static partial class Av1IntraEdgeUpsampler
{
    /// <summary>
    /// Applies the AV1 [-1, 9, 9, -1] interpolation kernel with Q4 rounding and clipping.
    /// </summary>
    internal readonly struct FourTapOperator : IEdgeUpsamplingOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Interpolate(int a, int b, int c, int d, int maximum)
            => Math.Clamp((((9 * (b + c)) - a - d) + 8) >> 4, 0, maximum);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Interpolate(Vector128<int> a, Vector128<int> b, Vector128<int> c, Vector128<int> d, int maximum)
        {
            // Signed 32-bit lanes preserve negative overshoot and the 12-bit central sum, which can reach 73710.
            Vector128<int> value = (((Vector128.Create(9) * (b + c)) - a - d) + Vector128.Create(8)) >> 4;
            return Vector128.Clamp(value, Vector128<int>.Zero, Vector128.Create(maximum));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Interpolate(Vector256<int> a, Vector256<int> b, Vector256<int> c, Vector256<int> d, int maximum)
        {
            // Signed 32-bit lanes preserve negative overshoot and the 12-bit central sum, which can reach 73710.
            Vector256<int> value = (((Vector256.Create(9) * (b + c)) - a - d) + Vector256.Create(8)) >> 4;
            return Vector256.Clamp(value, Vector256<int>.Zero, Vector256.Create(maximum));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> Interpolate(Vector512<int> a, Vector512<int> b, Vector512<int> c, Vector512<int> d, int maximum)
        {
            // Signed 32-bit lanes preserve negative overshoot and the 12-bit central sum, which can reach 73710.
            Vector512<int> value = (((Vector512.Create(9) * (b + c)) - a - d) + Vector512.Create(8)) >> 4;
            return Vector512.Clamp(value, Vector512<int>.Zero, Vector512.Create(maximum));
        }
    }
}
