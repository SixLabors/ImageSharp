// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.IntraBlockCopy;

/// <content>
/// Defines the closed horizontal intra-block-copy interpolation operator.
/// </content>
internal static partial class Av1IntraBlockCopyHorizontalPredictor
{
    /// <summary>
    /// Defines horizontal intra-block-copy filtering for scalar and SIMD lane groups.
    /// </summary>
    private interface IAv1IntraBlockCopyHorizontalOperator
    {
        /// <summary>
        /// Filters one 8-bit sample.
        /// </summary>
        /// <param name="left">The integer-position source sample.</param>
        /// <param name="right">The source sample one column to the right.</param>
        /// <returns>The filtered 8-bit sample.</returns>
        public static abstract byte Filter(byte left, byte right);

        /// <summary>
        /// Filters sixteen 8-bit samples in parallel.
        /// </summary>
        /// <param name="left">The integer-position source samples.</param>
        /// <param name="right">The source samples one column to the right.</param>
        /// <returns>The filtered 8-bit samples.</returns>
        public static abstract Vector128<byte> Filter(
            Vector128<byte> left,
            Vector128<byte> right);

        /// <summary>
        /// Filters thirty-two 8-bit samples in parallel.
        /// </summary>
        /// <param name="left">The integer-position source samples.</param>
        /// <param name="right">The source samples one column to the right.</param>
        /// <returns>The filtered 8-bit samples.</returns>
        public static abstract Vector256<byte> Filter(
            Vector256<byte> left,
            Vector256<byte> right);

        /// <summary>
        /// Filters sixty-four 8-bit samples in parallel.
        /// </summary>
        /// <param name="left">The integer-position source samples.</param>
        /// <param name="right">The source samples one column to the right.</param>
        /// <returns>The filtered 8-bit samples.</returns>
        public static abstract Vector512<byte> Filter(
            Vector512<byte> left,
            Vector512<byte> right);

        /// <summary>
        /// Filters one high-bit-depth sample.
        /// </summary>
        /// <param name="left">The integer-position source sample.</param>
        /// <param name="right">The source sample one column to the right.</param>
        /// <returns>The filtered high-bit-depth sample.</returns>
        public static abstract short Filter(short left, short right);

        /// <summary>
        /// Filters eight high-bit-depth samples in parallel.
        /// </summary>
        /// <param name="left">The integer-position source samples.</param>
        /// <param name="right">The source samples one column to the right.</param>
        /// <returns>The filtered high-bit-depth samples.</returns>
        public static abstract Vector128<short> Filter(
            Vector128<short> left,
            Vector128<short> right);

        /// <summary>
        /// Filters sixteen high-bit-depth samples in parallel.
        /// </summary>
        /// <param name="left">The integer-position source samples.</param>
        /// <param name="right">The source samples one column to the right.</param>
        /// <returns>The filtered high-bit-depth samples.</returns>
        public static abstract Vector256<short> Filter(
            Vector256<short> left,
            Vector256<short> right);

        /// <summary>
        /// Filters thirty-two high-bit-depth samples in parallel.
        /// </summary>
        /// <param name="left">The integer-position source samples.</param>
        /// <param name="right">The source samples one column to the right.</param>
        /// <returns>The filtered high-bit-depth samples.</returns>
        public static abstract Vector512<short> Filter(
            Vector512<short> left,
            Vector512<short> right);
    }

    /// <summary>
    /// Averages horizontally adjacent source samples for a half-sample horizontal phase.
    /// </summary>
    private readonly struct IntraBlockCopyHorizontalOperator : IAv1IntraBlockCopyHorizontalOperator
    {
        /// <inheritdoc/>
        public static byte Filter(byte left, byte right) => (byte)((left + right + 1) >> 1);

        /// <inheritdoc/>
        public static Vector128<byte> Filter(Vector128<byte> left, Vector128<byte> right)
            => AverageRounded(left, right);

        /// <inheritdoc/>
        public static Vector256<byte> Filter(Vector256<byte> left, Vector256<byte> right)
            => AverageRounded(left, right);

        /// <inheritdoc/>
        public static Vector512<byte> Filter(Vector512<byte> left, Vector512<byte> right)
            => AverageRounded(left, right);

        /// <inheritdoc/>
        public static short Filter(short left, short right) => (short)((left + right + 1) >> 1);

        /// <inheritdoc/>
        public static Vector128<short> Filter(Vector128<short> left, Vector128<short> right)
            => AverageRounded(left, right);

        /// <inheritdoc/>
        public static Vector256<short> Filter(Vector256<short> left, Vector256<short> right)
            => AverageRounded(left, right);

        /// <inheritdoc/>
        public static Vector512<short> Filter(Vector512<short> left, Vector512<short> right)
            => AverageRounded(left, right);

        /// <summary>
        /// Computes a rounded average without overflowing unsigned byte lanes.
        /// </summary>
        private static Vector128<byte> AverageRounded(Vector128<byte> left, Vector128<byte> right)
            => (left | right) - ((left ^ right) >> 1);

        /// <summary>
        /// Computes a rounded average without overflowing unsigned byte lanes.
        /// </summary>
        private static Vector256<byte> AverageRounded(Vector256<byte> left, Vector256<byte> right)
            => (left | right) - ((left ^ right) >> 1);

        /// <summary>
        /// Computes a rounded average without overflowing unsigned byte lanes.
        /// </summary>
        private static Vector512<byte> AverageRounded(Vector512<byte> left, Vector512<byte> right)
            => (left | right) - ((left ^ right) >> 1);

        /// <summary>
        /// Computes a rounded average without overflowing nonnegative high-bit-depth lanes.
        /// </summary>
        private static Vector128<short> AverageRounded(Vector128<short> left, Vector128<short> right)
        {
            Vector128<ushort> leftUnsigned = left.AsUInt16();
            Vector128<ushort> rightUnsigned = right.AsUInt16();

            // This identity computes ceil((a + b) / 2) without an overflowing lane-wise addition.
            return ((leftUnsigned | rightUnsigned) - ((leftUnsigned ^ rightUnsigned) >> 1)).AsInt16();
        }

        /// <summary>
        /// Computes a rounded average without overflowing nonnegative high-bit-depth lanes.
        /// </summary>
        private static Vector256<short> AverageRounded(Vector256<short> left, Vector256<short> right)
        {
            Vector256<ushort> leftUnsigned = left.AsUInt16();
            Vector256<ushort> rightUnsigned = right.AsUInt16();
            return ((leftUnsigned | rightUnsigned) - ((leftUnsigned ^ rightUnsigned) >> 1)).AsInt16();
        }

        /// <summary>
        /// Computes a rounded average without overflowing nonnegative high-bit-depth lanes.
        /// </summary>
        private static Vector512<short> AverageRounded(Vector512<short> left, Vector512<short> right)
        {
            Vector512<ushort> leftUnsigned = left.AsUInt16();
            Vector512<ushort> rightUnsigned = right.AsUInt16();
            return ((leftUnsigned | rightUnsigned) - ((leftUnsigned ^ rightUnsigned) >> 1)).AsInt16();
        }
    }
}
