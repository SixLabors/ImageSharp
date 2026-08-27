// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.IntraBlockCopy;

/// <content>
/// Defines the closed interpolation operators used by intra-block-copy prediction.
/// </content>
internal static partial class Av1IntraBlockCopyPredictor
{
    /// <summary>
    /// Averages horizontally adjacent source samples for a half-sample horizontal phase.
    /// </summary>
    private readonly struct HorizontalOperator : IAv1IntraBlockCopyOperator
    {
        /// <inheritdoc/>
        public static bool UsesRight => true;

        /// <inheritdoc/>
        public static bool UsesBottom => false;

        /// <inheritdoc/>
        public static byte Filter(byte topLeft, byte topRight, byte bottomLeft, byte bottomRight) => (byte)((topLeft + topRight + 1) >> 1);

        /// <inheritdoc/>
        public static Vector128<byte> Filter(
            Vector128<byte> topLeft,
            Vector128<byte> topRight,
            Vector128<byte> bottomLeft,
            Vector128<byte> bottomRight)
            => AverageRounded(topLeft, topRight);

        /// <inheritdoc/>
        public static Vector256<byte> Filter(
            Vector256<byte> topLeft,
            Vector256<byte> topRight,
            Vector256<byte> bottomLeft,
            Vector256<byte> bottomRight)
            => AverageRounded(topLeft, topRight);

        /// <inheritdoc/>
        public static Vector512<byte> Filter(
            Vector512<byte> topLeft,
            Vector512<byte> topRight,
            Vector512<byte> bottomLeft,
            Vector512<byte> bottomRight)
            => AverageRounded(topLeft, topRight);

        /// <inheritdoc/>
        public static short Filter(short topLeft, short topRight, short bottomLeft, short bottomRight) => (short)((topLeft + topRight + 1) >> 1);

        /// <inheritdoc/>
        public static Vector128<short> Filter(
            Vector128<short> topLeft,
            Vector128<short> topRight,
            Vector128<short> bottomLeft,
            Vector128<short> bottomRight)
            => AverageRounded(topLeft, topRight);

        /// <inheritdoc/>
        public static Vector256<short> Filter(
            Vector256<short> topLeft,
            Vector256<short> topRight,
            Vector256<short> bottomLeft,
            Vector256<short> bottomRight)
            => AverageRounded(topLeft, topRight);

        /// <inheritdoc/>
        public static Vector512<short> Filter(
            Vector512<short> topLeft,
            Vector512<short> topRight,
            Vector512<short> bottomLeft,
            Vector512<short> bottomRight)
            => AverageRounded(topLeft, topRight);
    }
}
