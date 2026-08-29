// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

internal static partial class HevcResidualReconstructor
{
    /// <summary>
    /// Applies the exact left shift used by high-bit-depth transform-skip reconstruction.
    /// </summary>
    private readonly struct LeftShiftTransformSkipOperator : ITransformSkipOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> Invoke(Vector512<int> values, int shift) => values << shift;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Invoke(Vector256<int> values, int shift) => values << shift;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Invoke(Vector128<int> values, int shift) => values << shift;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Invoke(int value, int shift) => value << shift;
    }
}
