// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <summary>
/// Closes the first pass over one sample storage type, so its block traversal calls the existing eight-bit or
/// high-bit-depth kernels without runtime type tests.
/// </summary>
internal static class Av1FirstPassOperator
{
    /// <summary>
    /// Forwards the sample work of the first pass to the closed kernels of one storage type.
    /// </summary>
    /// <typeparam name="TSample">The unsigned component storage type.</typeparam>
    public interface IOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Converts one stored sample to its native integer value. Reference: CONVERT_TO_SHORTPTR().
        /// </summary>
        /// <param name="value">The stored sample.</param>
        /// <returns>The sample value at native precision.</returns>
        public static abstract int ToInt32(TSample value);

        /// <summary>
        /// Loads four consecutive samples at native precision, one per 32-bit lane.
        /// </summary>
        /// <param name="source">The source plane.</param>
        /// <param name="index">The index of the first sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The samples.</returns>
        public static abstract Vector128<int> LoadWidened(ReadOnlySpan<TSample> source, int index, Vector128<int> lanes);

        /// <summary>
        /// Loads eight consecutive samples at native precision, one per 32-bit lane.
        /// </summary>
        /// <param name="source">The source plane.</param>
        /// <param name="index">The index of the first sample.</param>
        /// <param name="lanes">The overload-selection value.</param>
        /// <returns>The samples.</returns>
        public static abstract Vector256<int> LoadWidened(ReadOnlySpan<TSample> source, int index, Vector256<int> lanes);

        /// <summary>
        /// Measures the raw absolute differences of every row of a block, as the high-bit-depth form does before
        /// its precision shift. Reference: aom_sad16x16().
        /// </summary>
        /// <param name="source">The source samples at the block origin.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="reference">The reference samples at the displaced block origin.</param>
        /// <param name="referenceStride">The reference row stride.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <returns>The raw absolute-difference sum at native precision.</returns>
        public static abstract int SumAbsoluteDifferences(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int width,
            int height);

        /// <summary>
        /// Measures the raw signed and squared difference sums of a block, before any high-bit-depth rounding.
        /// Reference: highbd_variance64().
        /// </summary>
        /// <param name="source">The source samples at the block origin.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="reference">The reference samples at the displaced block origin.</param>
        /// <param name="referenceStride">The reference row stride.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="sum">The raw signed difference sum.</param>
        /// <param name="squares">The raw squared difference sum.</param>
        public static abstract void GetMoments(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int width,
            int height,
            out int sum,
            out long squares);

        /// <summary>
        /// Writes the source-minus-prediction residual of a block at either precision.
        /// Reference: aom_subtract_block().
        /// </summary>
        /// <param name="source">The source samples at the block origin.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="prediction">The prediction samples at the block origin.</param>
        /// <param name="predictionStride">The prediction row stride.</param>
        /// <param name="residual">The residual destination at the block origin.</param>
        /// <param name="residualStride">The residual row stride.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        public static abstract void Subtract(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            Span<short> residual,
            int residualStride,
            int width,
            int height);

        /// <summary>
        /// Predicts a square block from the mean of its available neighbors, falling back to one side or to the
        /// mid-range value. Reference: build_non_directional_intra_predictors().
        /// </summary>
        /// <param name="hasLeft">Whether the left column is available.</param>
        /// <param name="hasAbove">Whether the above row is available.</param>
        /// <param name="destination">The prediction destination at the block origin.</param>
        /// <param name="destinationStride">The destination row stride.</param>
        /// <param name="above">The contiguous above row.</param>
        /// <param name="left">The contiguous left column.</param>
        /// <param name="size">The block width and height.</param>
        /// <param name="bitDepth">The native sample precision.</param>
        public static abstract void PredictDc(
            bool hasLeft,
            bool hasAbove,
            Span<TSample> destination,
            int destinationStride,
            ReadOnlySpan<TSample> above,
            ReadOnlySpan<TSample> left,
            int size,
            int bitDepth);

        /// <summary>
        /// Adds the inverse transform of dequantized coefficients to the prediction in place, at either precision.
        /// Reference: av1_inv_txfm_add().
        /// </summary>
        /// <param name="coefficients">The dequantized coefficients in raster order.</param>
        /// <param name="reconstruction">The prediction and reconstruction samples at the block origin.</param>
        /// <param name="stride">The reconstruction row stride.</param>
        /// <param name="transformSize">The transform dimensions.</param>
        /// <param name="endOfBlock">The one-based final nonzero scan position.</param>
        /// <param name="bitDepth">The coded sample precision.</param>
        /// <param name="workspace">The reusable inverse-transform storage.</param>
        public static abstract void Reconstruct(
            ReadOnlySpan<int> coefficients,
            Span<TSample> reconstruction,
            int stride,
            Av1TransformSize transformSize,
            int endOfBlock,
            Av1BitDepth bitDepth,
            Span<int> workspace);

        /// <summary>
        /// Classifies a key frame for screen-content tools. Reference: estimate_screen_content().
        /// </summary>
        /// <param name="source">The coded source frame.</param>
        /// <param name="allowScreenContentTools">Receives whether palette tools are enabled.</param>
        /// <param name="allowIntraBlockCopy">Receives whether intra block copy is enabled.</param>
        /// <returns>Whether the frame is screen content for encoder decisions.</returns>
        public static abstract bool DetectScreenContent(
            Av1EncoderFrame<TSample> source,
            out bool allowScreenContentTools,
            out bool allowIntraBlockCopy);
    }

    /// <summary>
    /// Closes the first pass over eight-bit storage.
    /// </summary>
    public readonly struct ByteOperator : IOperator<byte>
    {
        /// <inheritdoc/>
        public static int ToInt32(byte value) => value;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> LoadWidened(ReadOnlySpan<byte> source, int index, Vector128<int> lanes)
            => Vector128.WidenLower(Vector128.WidenLower(Vector128.CreateScalar(MemoryMarshal.Read<uint>(source.Slice(index, 4))).AsByte())).AsInt32();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> LoadWidened(ReadOnlySpan<byte> source, int index, Vector256<int> lanes)
            => Vector256.WidenLower(Vector128.WidenLower(Vector128.CreateScalar(MemoryMarshal.Read<ulong>(source.Slice(index, 8))).AsByte()).ToVector256Unsafe()).AsInt32();

        /// <inheritdoc/>
        public static int SumAbsoluteDifferences(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> reference,
            int referenceStride,
            int width,
            int height)
            => Av1ResidualBuilder.SumAbsoluteDifferences(source, sourceStride, reference, referenceStride, width, height, 1);

        /// <inheritdoc/>
        public static void GetMoments(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> reference,
            int referenceStride,
            int width,
            int height,
            out int sum,
            out long squares)
            => Av1ResidualBuilder.GetMoments(source, sourceStride, reference, referenceStride, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static void Subtract(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            Span<short> residual,
            int residualStride,
            int width,
            int height)
            => Av1ResidualBuilder.Subtract(source, sourceStride, prediction, predictionStride, residual, residualStride, width, height);

        /// <inheritdoc/>
        public static void PredictDc(
            bool hasLeft,
            bool hasAbove,
            Span<byte> destination,
            int destinationStride,
            ReadOnlySpan<byte> above,
            ReadOnlySpan<byte> left,
            int size,
            int bitDepth)
            => Av1DcIntraPredictor.Predict(hasLeft, hasAbove, destination, destinationStride, above, left, size, size);

        /// <inheritdoc/>
        public static void Reconstruct(
            ReadOnlySpan<int> coefficients,
            Span<byte> reconstruction,
            int stride,
            Av1TransformSize transformSize,
            int endOfBlock,
            Av1BitDepth bitDepth,
            Span<int> workspace)
            => Av1InverseTransformer.Reconstruct8Bit(
                coefficients,
                reconstruction,
                stride,
                transformSize,
                Av1TransformType.DctDct,
                (int)Av1Plane.Y,
                endOfBlock,
                false,
                workspace);

        /// <inheritdoc/>
        public static bool DetectScreenContent(
            Av1EncoderFrame<byte> source,
            out bool allowScreenContentTools,
            out bool allowIntraBlockCopy)
            => Av1ScreenContentDetector.Detect(source, out allowScreenContentTools, out allowIntraBlockCopy);
    }

    /// <summary>
    /// Closes the first pass over high-bit-depth storage.
    /// </summary>
    public readonly struct UInt16Operator : IOperator<ushort>
    {
        /// <inheritdoc/>
        public static int ToInt32(ushort value) => value;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> LoadWidened(ReadOnlySpan<ushort> source, int index, Vector128<int> lanes)
            => Vector128.WidenLower(Vector128.CreateScalar(MemoryMarshal.Read<ulong>(MemoryMarshal.AsBytes(source.Slice(index, 4)))).AsUInt16()).AsInt32();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> LoadWidened(ReadOnlySpan<ushort> source, int index, Vector256<int> lanes)
            => Vector256.WidenLower(Vector128.Create(source.Slice(index, 8)).ToVector256Unsafe()).AsInt32();

        /// <inheritdoc/>
        public static int SumAbsoluteDifferences(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> reference,
            int referenceStride,
            int width,
            int height)
            => Av1ResidualBuilder.SumAbsoluteDifferences(source, sourceStride, reference, referenceStride, width, height, 1);

        /// <inheritdoc/>
        public static void GetMoments(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> reference,
            int referenceStride,
            int width,
            int height,
            out int sum,
            out long squares)
            => Av1ResidualBuilder.GetMoments(source, sourceStride, reference, referenceStride, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static void Subtract(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            Span<short> residual,
            int residualStride,
            int width,
            int height)
            => Av1ResidualBuilder.Subtract(source, sourceStride, prediction, predictionStride, residual, residualStride, width, height);

        /// <inheritdoc/>
        public static void PredictDc(
            bool hasLeft,
            bool hasAbove,
            Span<ushort> destination,
            int destinationStride,
            ReadOnlySpan<ushort> above,
            ReadOnlySpan<ushort> left,
            int size,
            int bitDepth)
        {
            // Samples never exceed twelve bits, so the signed view the shared predictor takes holds them unchanged.
            Av1DcIntraPredictor.Predict(
                hasLeft,
                hasAbove,
                MemoryMarshal.Cast<ushort, short>(destination),
                destinationStride,
                MemoryMarshal.Cast<ushort, short>(above),
                MemoryMarshal.Cast<ushort, short>(left),
                size,
                size,
                bitDepth);
        }

        /// <inheritdoc/>
        public static void Reconstruct(
            ReadOnlySpan<int> coefficients,
            Span<ushort> reconstruction,
            int stride,
            Av1TransformSize transformSize,
            int endOfBlock,
            Av1BitDepth bitDepth,
            Span<int> workspace)
            => Av1InverseTransformer.ReconstructHighBitDepth(
                coefficients,
                MemoryMarshal.Cast<ushort, short>(reconstruction),
                stride,
                transformSize,
                Av1TransformType.DctDct,
                (int)Av1Plane.Y,
                endOfBlock,
                false,
                bitDepth,
                workspace);

        /// <inheritdoc/>
        public static bool DetectScreenContent(
            Av1EncoderFrame<ushort> source,
            out bool allowScreenContentTools,
            out bool allowIntraBlockCopy)
            => Av1ScreenContentDetector.Detect(source, out allowScreenContentTools, out allowIntraBlockCopy);
    }
}
