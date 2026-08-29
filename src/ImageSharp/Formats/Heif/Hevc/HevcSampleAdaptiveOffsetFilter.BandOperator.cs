// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

internal static partial class HevcSampleAdaptiveOffsetFilter
{
    /// <summary>
    /// Classifies samples by one of thirty-two most-significant-value bands.
    /// </summary>
    private readonly struct BandOperator : ISampleClassifier
    {
        /// <inheritdoc/>
        public static bool UsesNeighbors => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Classify(
            Vector512<short> current,
            Vector512<short> neighbor0,
            Vector512<short> neighbor1,
            in KernelParameters kernel)
            => (Vector512.ShiftRightArithmetic(current, kernel.BandShift) - Vector512.Create(kernel.BandPosition)) & Vector512.Create((short)31);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Classify(
            Vector256<short> current,
            Vector256<short> neighbor0,
            Vector256<short> neighbor1,
            in KernelParameters kernel)
            => (Vector256.ShiftRightArithmetic(current, kernel.BandShift) - Vector256.Create(kernel.BandPosition)) & Vector256.Create((short)31);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> Classify(
            Vector128<short> current,
            Vector128<short> neighbor0,
            Vector128<short> neighbor1,
            in KernelParameters kernel)
            => (Vector128.ShiftRightArithmetic(current, kernel.BandShift) - Vector128.Create(kernel.BandPosition)) & Vector128.Create((short)31);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Classify(short current, short neighbor0, short neighbor1, in KernelParameters kernel)
            => ((current >> kernel.BandShift) - kernel.BandPosition) & 31;
    }
}
