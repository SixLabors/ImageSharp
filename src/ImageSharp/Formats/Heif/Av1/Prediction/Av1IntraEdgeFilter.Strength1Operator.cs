// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal static partial class Av1IntraEdgeFilter
{
    /// <summary>
    /// Applies the strength-1 three-tap edge smoothing kernel.
    /// </summary>
    internal readonly struct Strength1Operator : IEdgeFilterOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Apply(int a, int b, int c, int d, int e)
            => (b + (c << 1) + d + 2) >> 2;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> Apply(Vector128<ushort> a, Vector128<ushort> b, Vector128<ushort> c, Vector128<ushort> d, Vector128<ushort> e)
            => (b + (c << 1) + d + Vector128.Create((ushort)2)) >> 2;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> Apply(Vector256<ushort> a, Vector256<ushort> b, Vector256<ushort> c, Vector256<ushort> d, Vector256<ushort> e)
            => (b + (c << 1) + d + Vector256.Create((ushort)2)) >> 2;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> Apply(Vector512<ushort> a, Vector512<ushort> b, Vector512<ushort> c, Vector512<ushort> d, Vector512<ushort> e)
            => (b + (c << 1) + d + Vector512.Create((ushort)2)) >> 2;
    }
}
