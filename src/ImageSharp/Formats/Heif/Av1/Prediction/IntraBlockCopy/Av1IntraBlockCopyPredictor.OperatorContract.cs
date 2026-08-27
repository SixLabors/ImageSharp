// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.IntraBlockCopy;

/// <content>
/// Defines the scalar and SIMD contract for closed intra-block-copy filter operators.
/// </content>
internal static partial class Av1IntraBlockCopyPredictor
{
    /// <summary>
    /// Defines lane-wise arithmetic for one intra-block-copy filter phase.
    /// </summary>
    /// <remarks>
    /// Every SIMD lane corresponds to one output column. The generic traversal supplies the integer source sample and
    /// its right, lower, and lower-right neighbors; closed operator types allow the JIT to remove unused source loads.
    /// </remarks>
    private interface IOperator
    {
        /// <summary>
        /// Gets a value indicating whether the operator consumes the source sample to the right.
        /// </summary>
        public static abstract bool UsesRight { get; }

        /// <summary>
        /// Gets a value indicating whether the operator consumes the source sample on the following row.
        /// </summary>
        public static abstract bool UsesBottom { get; }

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
}
