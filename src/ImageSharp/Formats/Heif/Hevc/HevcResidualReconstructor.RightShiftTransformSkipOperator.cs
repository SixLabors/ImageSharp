// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

internal static partial class HevcResidualReconstructor
{
    /// <summary>
    /// Applies the rounded right shift used by ordinary transform-skip reconstruction.
    /// </summary>
    private readonly struct RightShiftTransformSkipOperator : ITransformSkipOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> Invoke(Vector512<int> values, int shift)
            => shift == 0 ? values : (values + Vector512.Create(1 << (shift - 1))) >> shift;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Invoke(Vector256<int> values, int shift)
            => shift == 0 ? values : (values + Vector256.Create(1 << (shift - 1))) >> shift;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Invoke(Vector128<int> values, int shift)
            => shift == 0 ? values : (values + Vector128.Create(1 << (shift - 1))) >> shift;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Invoke(int value, int shift) => shift == 0 ? value : (value + (1 << (shift - 1))) >> shift;
    }
}
