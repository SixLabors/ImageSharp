// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.IntraBlockCopy;

/// <content>
/// Defines the closed bilinear intra-block-copy interpolation operator.
/// </content>
internal static partial class Av1IntraBlockCopyBilinearPredictor
{
    /// <summary>
    /// Defines bilinear intra-block-copy filtering for scalar and SIMD lane groups.
    /// </summary>
    private interface IAv1IntraBlockCopyBilinearOperator
    {
        /// <summary>
        /// Filters one 8-bit sample.
        /// </summary>
        /// <param name="topLeft">The integer-position source sample.</param>
        /// <param name="topRight">The source sample one column to the right.</param>
        /// <param name="bottomLeft">The source sample one row below.</param>
        /// <param name="bottomRight">The source sample one row below and one column to the right.</param>
        /// <returns>The filtered 8-bit sample.</returns>
        public static abstract byte Filter(byte topLeft, byte topRight, byte bottomLeft, byte bottomRight);

        /// <summary>
        /// Filters sixteen 8-bit samples in parallel.
        /// </summary>
        /// <param name="topLeft">The integer-position source samples.</param>
        /// <param name="topRight">The source samples one column to the right.</param>
        /// <param name="bottomLeft">The source samples one row below.</param>
        /// <param name="bottomRight">The source samples one row below and one column to the right.</param>
        /// <returns>The filtered 8-bit samples.</returns>
        public static abstract Vector128<byte> Filter(
            Vector128<byte> topLeft,
            Vector128<byte> topRight,
            Vector128<byte> bottomLeft,
            Vector128<byte> bottomRight);

        /// <summary>
        /// Filters thirty-two 8-bit samples in parallel.
        /// </summary>
        /// <param name="topLeft">The integer-position source samples.</param>
        /// <param name="topRight">The source samples one column to the right.</param>
        /// <param name="bottomLeft">The source samples one row below.</param>
        /// <param name="bottomRight">The source samples one row below and one column to the right.</param>
        /// <returns>The filtered 8-bit samples.</returns>
        public static abstract Vector256<byte> Filter(
            Vector256<byte> topLeft,
            Vector256<byte> topRight,
            Vector256<byte> bottomLeft,
            Vector256<byte> bottomRight);

        /// <summary>
        /// Filters sixty-four 8-bit samples in parallel.
        /// </summary>
        /// <param name="topLeft">The integer-position source samples.</param>
        /// <param name="topRight">The source samples one column to the right.</param>
        /// <param name="bottomLeft">The source samples one row below.</param>
        /// <param name="bottomRight">The source samples one row below and one column to the right.</param>
        /// <returns>The filtered 8-bit samples.</returns>
        public static abstract Vector512<byte> Filter(
            Vector512<byte> topLeft,
            Vector512<byte> topRight,
            Vector512<byte> bottomLeft,
            Vector512<byte> bottomRight);

        /// <summary>
        /// Filters one high-bit-depth sample.
        /// </summary>
        /// <param name="topLeft">The integer-position source sample.</param>
        /// <param name="topRight">The source sample one column to the right.</param>
        /// <param name="bottomLeft">The source sample one row below.</param>
        /// <param name="bottomRight">The source sample one row below and one column to the right.</param>
        /// <returns>The filtered high-bit-depth sample.</returns>
        public static abstract short Filter(short topLeft, short topRight, short bottomLeft, short bottomRight);

        /// <summary>
        /// Filters eight high-bit-depth samples in parallel.
        /// </summary>
        /// <param name="topLeft">The integer-position source samples.</param>
        /// <param name="topRight">The source samples one column to the right.</param>
        /// <param name="bottomLeft">The source samples one row below.</param>
        /// <param name="bottomRight">The source samples one row below and one column to the right.</param>
        /// <returns>The filtered high-bit-depth samples.</returns>
        public static abstract Vector128<short> Filter(
            Vector128<short> topLeft,
            Vector128<short> topRight,
            Vector128<short> bottomLeft,
            Vector128<short> bottomRight);

        /// <summary>
        /// Filters sixteen high-bit-depth samples in parallel.
        /// </summary>
        /// <param name="topLeft">The integer-position source samples.</param>
        /// <param name="topRight">The source samples one column to the right.</param>
        /// <param name="bottomLeft">The source samples one row below.</param>
        /// <param name="bottomRight">The source samples one row below and one column to the right.</param>
        /// <returns>The filtered high-bit-depth samples.</returns>
        public static abstract Vector256<short> Filter(
            Vector256<short> topLeft,
            Vector256<short> topRight,
            Vector256<short> bottomLeft,
            Vector256<short> bottomRight);

        /// <summary>
        /// Filters thirty-two high-bit-depth samples in parallel.
        /// </summary>
        /// <param name="topLeft">The integer-position source samples.</param>
        /// <param name="topRight">The source samples one column to the right.</param>
        /// <param name="bottomLeft">The source samples one row below.</param>
        /// <param name="bottomRight">The source samples one row below and one column to the right.</param>
        /// <returns>The filtered high-bit-depth samples.</returns>
        public static abstract Vector512<short> Filter(
            Vector512<short> topLeft,
            Vector512<short> topRight,
            Vector512<short> bottomLeft,
            Vector512<short> bottomRight);
    }

    /// <summary>
    /// Applies the separable two-dimensional interpolation required when both source axes have a half-sample phase.
    /// </summary>
    /// <remarks>
    /// The offsets in the reference decoder's separable two-pass implementation cancel algebraically to
    /// <c>(topLeft + topRight + bottomLeft + bottomRight + 2) &gt;&gt; 2</c>, so the closed operator produces the exact
    /// result directly without an intermediate image buffer.
    ///
    /// Byte lanes widen to unsigned 16-bit halves before the four-source sum, while high-bit-depth lanes widen to
    /// unsigned 32-bit halves. Narrowing recombines those halves in source-column order after the rounded result has
    /// returned to the original sample range.
    /// </remarks>
    private readonly struct IntraBlockCopyBilinearOperator : IAv1IntraBlockCopyBilinearOperator
    {
        /// <inheritdoc/>
        public static byte Filter(byte topLeft, byte topRight, byte bottomLeft, byte bottomRight)
            => (byte)((topLeft + topRight + bottomLeft + bottomRight + 2) >> 2);

        /// <inheritdoc/>
        public static Vector128<byte> Filter(
            Vector128<byte> topLeft,
            Vector128<byte> topRight,
            Vector128<byte> bottomLeft,
            Vector128<byte> bottomRight)
        {
            (Vector128<ushort> topLeftLow, Vector128<ushort> topLeftHigh) = Vector128.Widen(topLeft);
            (Vector128<ushort> topRightLow, Vector128<ushort> topRightHigh) = Vector128.Widen(topRight);
            (Vector128<ushort> bottomLeftLow, Vector128<ushort> bottomLeftHigh) = Vector128.Widen(bottomLeft);
            (Vector128<ushort> bottomRightLow, Vector128<ushort> bottomRightHigh) = Vector128.Widen(bottomRight);

            // Four byte samples can sum to 1020, so ushort lanes preserve the complete value before AV1's +2
            // rounding term and divide-by-four shift. Narrowing is exact because the result remains in byte range.
            Vector128<ushort> low = (topLeftLow + topRightLow + bottomLeftLow + bottomRightLow + Vector128.Create((ushort)2)) >> 2;
            Vector128<ushort> high = (topLeftHigh + topRightHigh + bottomLeftHigh + bottomRightHigh + Vector128.Create((ushort)2)) >> 2;
            return Vector128.Narrow(low, high);
        }

        /// <inheritdoc/>
        public static Vector256<byte> Filter(
            Vector256<byte> topLeft,
            Vector256<byte> topRight,
            Vector256<byte> bottomLeft,
            Vector256<byte> bottomRight)
        {
            (Vector256<ushort> topLeftLow, Vector256<ushort> topLeftHigh) = Vector256.Widen(topLeft);
            (Vector256<ushort> topRightLow, Vector256<ushort> topRightHigh) = Vector256.Widen(topRight);
            (Vector256<ushort> bottomLeftLow, Vector256<ushort> bottomLeftHigh) = Vector256.Widen(bottomLeft);
            (Vector256<ushort> bottomRightLow, Vector256<ushort> bottomRightHigh) = Vector256.Widen(bottomRight);
            Vector256<ushort> rounding = Vector256.Create((ushort)2);
            Vector256<ushort> low = (topLeftLow + topRightLow + bottomLeftLow + bottomRightLow + rounding) >> 2;
            Vector256<ushort> high = (topLeftHigh + topRightHigh + bottomLeftHigh + bottomRightHigh + rounding) >> 2;
            return Vector256.Narrow(low, high);
        }

        /// <inheritdoc/>
        public static Vector512<byte> Filter(
            Vector512<byte> topLeft,
            Vector512<byte> topRight,
            Vector512<byte> bottomLeft,
            Vector512<byte> bottomRight)
        {
            (Vector512<ushort> topLeftLow, Vector512<ushort> topLeftHigh) = Vector512.Widen(topLeft);
            (Vector512<ushort> topRightLow, Vector512<ushort> topRightHigh) = Vector512.Widen(topRight);
            (Vector512<ushort> bottomLeftLow, Vector512<ushort> bottomLeftHigh) = Vector512.Widen(bottomLeft);
            (Vector512<ushort> bottomRightLow, Vector512<ushort> bottomRightHigh) = Vector512.Widen(bottomRight);
            Vector512<ushort> rounding = Vector512.Create((ushort)2);
            Vector512<ushort> low = (topLeftLow + topRightLow + bottomLeftLow + bottomRightLow + rounding) >> 2;
            Vector512<ushort> high = (topLeftHigh + topRightHigh + bottomLeftHigh + bottomRightHigh + rounding) >> 2;
            return Vector512.Narrow(low, high);
        }

        /// <inheritdoc/>
        public static short Filter(short topLeft, short topRight, short bottomLeft, short bottomRight)
            => (short)((topLeft + topRight + bottomLeft + bottomRight + 2) >> 2);

        /// <inheritdoc/>
        public static Vector128<short> Filter(
            Vector128<short> topLeft,
            Vector128<short> topRight,
            Vector128<short> bottomLeft,
            Vector128<short> bottomRight)
        {
            (Vector128<uint> topLeftLow, Vector128<uint> topLeftHigh) = Vector128.Widen(topLeft.AsUInt16());
            (Vector128<uint> topRightLow, Vector128<uint> topRightHigh) = Vector128.Widen(topRight.AsUInt16());
            (Vector128<uint> bottomLeftLow, Vector128<uint> bottomLeftHigh) = Vector128.Widen(bottomLeft.AsUInt16());
            (Vector128<uint> bottomRightLow, Vector128<uint> bottomRightHigh) = Vector128.Widen(bottomRight.AsUInt16());

            // High-bit-depth storage is signed for integration with transform code, but reconstructed samples are
            // nonnegative. Unsigned widening therefore preserves 10- and 12-bit values through the four-input sum.
            Vector128<uint> low = (topLeftLow + topRightLow + bottomLeftLow + bottomRightLow + Vector128.Create(2U)) >> 2;
            Vector128<uint> high = (topLeftHigh + topRightHigh + bottomLeftHigh + bottomRightHigh + Vector128.Create(2U)) >> 2;
            return Vector128.Narrow(low, high).AsInt16();
        }

        /// <inheritdoc/>
        public static Vector256<short> Filter(
            Vector256<short> topLeft,
            Vector256<short> topRight,
            Vector256<short> bottomLeft,
            Vector256<short> bottomRight)
        {
            (Vector256<uint> topLeftLow, Vector256<uint> topLeftHigh) = Vector256.Widen(topLeft.AsUInt16());
            (Vector256<uint> topRightLow, Vector256<uint> topRightHigh) = Vector256.Widen(topRight.AsUInt16());
            (Vector256<uint> bottomLeftLow, Vector256<uint> bottomLeftHigh) = Vector256.Widen(bottomLeft.AsUInt16());
            (Vector256<uint> bottomRightLow, Vector256<uint> bottomRightHigh) = Vector256.Widen(bottomRight.AsUInt16());
            Vector256<uint> rounding = Vector256.Create(2U);
            Vector256<uint> low = (topLeftLow + topRightLow + bottomLeftLow + bottomRightLow + rounding) >> 2;
            Vector256<uint> high = (topLeftHigh + topRightHigh + bottomLeftHigh + bottomRightHigh + rounding) >> 2;
            return Vector256.Narrow(low, high).AsInt16();
        }

        /// <inheritdoc/>
        public static Vector512<short> Filter(
            Vector512<short> topLeft,
            Vector512<short> topRight,
            Vector512<short> bottomLeft,
            Vector512<short> bottomRight)
        {
            (Vector512<uint> topLeftLow, Vector512<uint> topLeftHigh) = Vector512.Widen(topLeft.AsUInt16());
            (Vector512<uint> topRightLow, Vector512<uint> topRightHigh) = Vector512.Widen(topRight.AsUInt16());
            (Vector512<uint> bottomLeftLow, Vector512<uint> bottomLeftHigh) = Vector512.Widen(bottomLeft.AsUInt16());
            (Vector512<uint> bottomRightLow, Vector512<uint> bottomRightHigh) = Vector512.Widen(bottomRight.AsUInt16());
            Vector512<uint> rounding = Vector512.Create(2U);
            Vector512<uint> low = (topLeftLow + topRightLow + bottomLeftLow + bottomRightLow + rounding) >> 2;
            Vector512<uint> high = (topLeftHigh + topRightHigh + bottomLeftHigh + bottomRightHigh + rounding) >> 2;
            return Vector512.Narrow(low, high).AsInt16();
        }
    }
}
