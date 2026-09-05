// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal static partial class Av1IntraEdgeFilter
{
    /// <summary>
    /// Applies the strength-3 five-tap edge smoothing kernel.
    /// </summary>
    internal readonly struct Strength3Operator : IEdgeFilterOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Apply(int a, int b, int c, int d, int e)
            => (a + ((b + c + d) << 1) + e + 4) >> 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> Apply(Vector128<ushort> a, Vector128<ushort> b, Vector128<ushort> c, Vector128<ushort> d, Vector128<ushort> e)
            => (a + ((b + c + d) << 1) + e + Vector128.Create((ushort)4)) >> 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> Apply(Vector256<ushort> a, Vector256<ushort> b, Vector256<ushort> c, Vector256<ushort> d, Vector256<ushort> e)
            => (a + ((b + c + d) << 1) + e + Vector256.Create((ushort)4)) >> 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> Apply(Vector512<ushort> a, Vector512<ushort> b, Vector512<ushort> c, Vector512<ushort> d, Vector512<ushort> e)
            => (a + ((b + c + d) << 1) + e + Vector512.Create((ushort)4)) >> 3;
    }
}
