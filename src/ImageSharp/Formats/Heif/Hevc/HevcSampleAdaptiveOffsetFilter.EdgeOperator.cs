// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

internal static partial class HevcSampleAdaptiveOffsetFilter
{
    /// <summary>
    /// Classifies samples by the sum of their signs relative to two directional neighbors.
    /// </summary>
    private readonly struct EdgeOperator : ISampleClassifier
    {
        /// <inheritdoc/>
        public static bool UsesNeighbors => true;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Classify(
            Vector512<short> current,
            Vector512<short> neighbor0,
            Vector512<short> neighbor1,
            in KernelParameters kernel)
        {
            // Each comparison pair produces -1, 0, or 1. Adding two maps the normative edge classes onto the
            // contiguous zero-through-four offset-table indices used by the selection kernel.
            Vector512<short> one = Vector512.Create((short)1);
            Vector512<short> sign0 = (Vector512.GreaterThan(current, neighbor0) & one) - (Vector512.LessThan(current, neighbor0) & one);
            Vector512<short> sign1 = (Vector512.GreaterThan(current, neighbor1) & one) - (Vector512.LessThan(current, neighbor1) & one);
            return sign0 + sign1 + Vector512.Create((short)2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Classify(
            Vector256<short> current,
            Vector256<short> neighbor0,
            Vector256<short> neighbor1,
            in KernelParameters kernel)
        {
            Vector256<short> one = Vector256.Create((short)1);
            Vector256<short> sign0 = (Vector256.GreaterThan(current, neighbor0) & one) - (Vector256.LessThan(current, neighbor0) & one);
            Vector256<short> sign1 = (Vector256.GreaterThan(current, neighbor1) & one) - (Vector256.LessThan(current, neighbor1) & one);
            return sign0 + sign1 + Vector256.Create((short)2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> Classify(
            Vector128<short> current,
            Vector128<short> neighbor0,
            Vector128<short> neighbor1,
            in KernelParameters kernel)
        {
            Vector128<short> one = Vector128.Create((short)1);
            Vector128<short> sign0 = (Vector128.GreaterThan(current, neighbor0) & one) - (Vector128.LessThan(current, neighbor0) & one);
            Vector128<short> sign1 = (Vector128.GreaterThan(current, neighbor1) & one) - (Vector128.LessThan(current, neighbor1) & one);
            return sign0 + sign1 + Vector128.Create((short)2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Classify(short current, short neighbor0, short neighbor1, in KernelParameters kernel)
            => Math.Sign(current - neighbor0) + Math.Sign(current - neighbor1) + 2;
    }
}
