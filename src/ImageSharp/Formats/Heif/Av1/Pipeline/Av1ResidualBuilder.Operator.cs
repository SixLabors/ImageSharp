// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Defines the sample-width-specific arithmetic used by <see cref="Av1ResidualBuilder"/>.
/// </content>
internal static partial class Av1ResidualBuilder
{
    /// <summary>
    /// Defines one AV1 source-minus-prediction operation across hardware widths.
    /// </summary>
    /// <typeparam name="TSample">The source and prediction sample type.</typeparam>
    internal interface IResidualOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Subtracts eight or sixteen source and prediction samples.
        /// </summary>
        /// <param name="source">The source samples.</param>
        /// <param name="prediction">The prediction samples.</param>
        /// <param name="upper">The upper residual lanes when the inputs contain 8-bit samples.</param>
        /// <returns>The lower residual lanes.</returns>
        public static abstract Vector128<short> Subtract(Vector128<TSample> source, Vector128<TSample> prediction, out Vector128<short> upper);

        /// <summary>
        /// Subtracts sixteen or thirty-two source and prediction samples.
        /// </summary>
        /// <param name="source">The source samples.</param>
        /// <param name="prediction">The prediction samples.</param>
        /// <param name="upper">The upper residual lanes when the inputs contain 8-bit samples.</param>
        /// <returns>The lower residual lanes.</returns>
        public static abstract Vector256<short> Subtract(Vector256<TSample> source, Vector256<TSample> prediction, out Vector256<short> upper);

        /// <summary>
        /// Subtracts thirty-two or sixty-four source and prediction samples.
        /// </summary>
        /// <param name="source">The source samples.</param>
        /// <param name="prediction">The prediction samples.</param>
        /// <param name="upper">The upper residual lanes when the inputs contain 8-bit samples.</param>
        /// <returns>The lower residual lanes.</returns>
        public static abstract Vector512<short> Subtract(Vector512<TSample> source, Vector512<TSample> prediction, out Vector512<short> upper);

        /// <summary>
        /// Subtracts one source and prediction sample.
        /// </summary>
        /// <param name="source">The source sample.</param>
        /// <param name="prediction">The prediction sample.</param>
        /// <returns>The signed residual.</returns>
        public static abstract short Subtract(TSample source, TSample prediction);
    }

    /// <summary>
    /// Widens 8-bit samples before subtraction so every residual is represented without precision loss.
    /// </summary>
    internal readonly struct ByteOperator : IResidualOperator<byte>
    {
        /// <inheritdoc/>
        public static Vector128<short> Subtract(Vector128<byte> source, Vector128<byte> prediction, out Vector128<short> upper)
        {
            Vector128<short> lower = Vector128.WidenLower(source).AsInt16() - Vector128.WidenLower(prediction).AsInt16();
            upper = Vector128.WidenUpper(source).AsInt16() - Vector128.WidenUpper(prediction).AsInt16();
            return lower;
        }

        /// <inheritdoc/>
        public static Vector256<short> Subtract(Vector256<byte> source, Vector256<byte> prediction, out Vector256<short> upper)
        {
            Vector256<short> lower = Vector256.WidenLower(source).AsInt16() - Vector256.WidenLower(prediction).AsInt16();
            upper = Vector256.WidenUpper(source).AsInt16() - Vector256.WidenUpper(prediction).AsInt16();
            return lower;
        }

        /// <inheritdoc/>
        public static Vector512<short> Subtract(Vector512<byte> source, Vector512<byte> prediction, out Vector512<short> upper)
        {
            Vector512<short> lower = Vector512.WidenLower(source).AsInt16() - Vector512.WidenLower(prediction).AsInt16();
            upper = Vector512.WidenUpper(source).AsInt16() - Vector512.WidenUpper(prediction).AsInt16();
            return lower;
        }

        /// <inheritdoc/>
        public static short Subtract(byte source, byte prediction) => (short)(source - prediction);
    }

    /// <summary>
    /// Subtracts high-bit-depth samples directly because AV1's 10-bit and 12-bit ranges fit signed-short lanes.
    /// </summary>
    internal readonly struct UInt16Operator : IResidualOperator<ushort>
    {
        /// <inheritdoc/>
        public static Vector128<short> Subtract(Vector128<ushort> source, Vector128<ushort> prediction, out Vector128<short> upper)
        {
            upper = default;
            return source.AsInt16() - prediction.AsInt16();
        }

        /// <inheritdoc/>
        public static Vector256<short> Subtract(Vector256<ushort> source, Vector256<ushort> prediction, out Vector256<short> upper)
        {
            upper = default;
            return source.AsInt16() - prediction.AsInt16();
        }

        /// <inheritdoc/>
        public static Vector512<short> Subtract(Vector512<ushort> source, Vector512<ushort> prediction, out Vector512<short> upper)
        {
            upper = default;
            return source.AsInt16() - prediction.AsInt16();
        }

        /// <inheritdoc/>
        public static short Subtract(ushort source, ushort prediction) => (short)(source - prediction);
    }
}
