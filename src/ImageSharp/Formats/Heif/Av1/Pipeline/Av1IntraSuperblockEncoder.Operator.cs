// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.IntraBlockCopy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Defines the sample-storage operations used by fixed intra superblock traversal.
/// </content>
internal static partial class Av1IntraSuperblockEncoder
{
    /// <summary>
    /// Defines type-specific block encoding without coupling traversal to sample storage width.
    /// </summary>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    internal interface IBlockEncodingOperator<TSample> :
        Av1IntraBlockCopySearchIndex.ISearchOperation<TSample>,
        Av1MotionSearchBase.IMotionSearchOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Gets temporary contiguous storage for left reference samples.
        /// </summary>
        /// <param name="residual">The reusable residual workspace.</param>
        /// <param name="length">The number of reference samples.</param>
        /// <returns>The writable reference span.</returns>
        public static abstract Span<TSample> GetLeftReference(Span<short> residual, int length);

        /// <summary>
        /// Converts a valid sample value to the native plane storage type.
        /// </summary>
        /// <param name="value">The sample value.</param>
        /// <returns>The converted sample.</returns>
        public static abstract TSample CreateSample(int value);

        /// <summary>
        /// Copies active palette-search samples into contiguous signed storage.
        /// </summary>
        /// <param name="source">The coded source plane.</param>
        /// <param name="blockOrigin">The block origin in plane samples.</param>
        /// <param name="rows">The active row count.</param>
        /// <param name="columns">The active column count.</param>
        /// <param name="samples">The contiguous sample destination.</param>
        public static abstract void CopyPaletteSamples(
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            int rows,
            int columns,
            Span<short> samples);

        /// <summary>
        /// Builds palette prediction and the matching source residual for transform search.
        /// </summary>
        /// <param name="source">The coded source plane.</param>
        /// <param name="blockOrigin">The block origin in plane samples.</param>
        /// <param name="paletteColors">The palette colors in index order.</param>
        /// <param name="colorIndexMap">The complete padded color-index map.</param>
        /// <param name="prediction">The contiguous prediction destination.</param>
        /// <param name="residual">The contiguous source-minus-prediction destination.</param>
        /// <param name="transformSize">The prediction dimensions.</param>
        public static abstract void PreparePalette(
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            ReadOnlySpan<ushort> paletteColors,
            Buffer2DRegion<byte> colorIndexMap,
            Span<TSample> prediction,
            Span<short> residual,
            Av1TransformSize transformSize);

        /// <summary>
        /// Builds the zero-mean Q3 luma surface shared by chroma-from-luma candidates.
        /// </summary>
        /// <param name="reconstruction">The coded reconstructed luma plane.</param>
        /// <param name="blockOrigin">The luma block origin in plane samples.</param>
        /// <param name="lumaQ3">The fixed-stride Q3 predictor workspace.</param>
        /// <param name="transformSize">The chroma transform dimensions.</param>
        /// <param name="subsamplingX">Whether luma is subsampled horizontally for chroma.</param>
        /// <param name="subsamplingY">Whether luma is subsampled vertically for chroma.</param>
        public static abstract void PrepareChromaFromLuma(
            Buffer2DRegion<TSample> reconstruction,
            Point blockOrigin,
            Span<short> lumaQ3,
            Av1TransformSize transformSize,
            bool subsamplingX,
            bool subsamplingY);

        /// <summary>
        /// Computes the DC predictor shared by every chroma-from-luma alpha candidate.
        /// </summary>
        /// <param name="reconstruction">The contiguous candidate reconstruction.</param>
        /// <param name="above">The top reference samples.</param>
        /// <param name="left">The left reference samples.</param>
        /// <param name="hasLeft">Whether the left reference is available.</param>
        /// <param name="hasAbove">Whether the top reference is available.</param>
        /// <param name="transformSize">The chroma transform dimensions.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        public static abstract void PrepareChromaFromLumaDc(
            Span<TSample> reconstruction,
            ReadOnlySpan<TSample> above,
            ReadOnlySpan<TSample> left,
            bool hasLeft,
            bool hasAbove,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth);

        /// <summary>
        /// Encodes and reconstructs one DC intra transform block.
        /// </summary>
        /// <param name="workspace">The reusable block workspace.</param>
        /// <param name="source">The coded source plane.</param>
        /// <param name="reconstruction">The coded reconstruction plane.</param>
        /// <param name="blockOrigin">The transform-block origin in plane samples.</param>
        /// <param name="above">The top reference samples.</param>
        /// <param name="left">The left reference samples.</param>
        /// <param name="hasLeft">Whether the left reference is available.</param>
        /// <param name="hasAbove">Whether the top reference is available.</param>
        /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
        /// <param name="transformSize">The transform dimensions.</param>
        /// <param name="qIndex">The effective segment quantizer index.</param>
        /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
        /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
        /// <param name="plane">The component plane containing the block.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        /// <param name="state">The retained transform state.</param>
        public static abstract void Encode(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<TSample> source,
            Buffer2DRegion<TSample> reconstruction,
            Point blockOrigin,
            ReadOnlySpan<TSample> above,
            ReadOnlySpan<TSample> left,
            bool hasLeft,
            bool hasAbove,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1Plane plane,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state);

        /// <summary>
        /// Encodes one intra candidate into contiguous decision scratch.
        /// </summary>
        /// <param name="workspace">The reusable block workspace.</param>
        /// <param name="source">The coded source plane.</param>
        /// <param name="blockOrigin">The transform-block origin in plane samples.</param>
        /// <param name="reconstruction">The contiguous candidate reconstruction.</param>
        /// <param name="above">The top reference samples, with prefix storage for the shared corner.</param>
        /// <param name="left">The left reference samples.</param>
        /// <param name="hasLeft">Whether the left reference is available.</param>
        /// <param name="hasAbove">Whether the top reference is available.</param>
        /// <param name="mode">The intra prediction mode.</param>
        /// <param name="angleDelta">The signed directional-angle adjustment.</param>
        /// <param name="enableIntraEdgeFilter">Whether sequence syntax enables directional edge filtering.</param>
        /// <param name="smoothIntraEdges">Whether a relevant neighboring block uses smooth prediction.</param>
        /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
        /// <param name="transformSize">The transform dimensions.</param>
        /// <param name="transformType">The compound transform applied to the residual.</param>
        /// <param name="plane">The component plane containing the block.</param>
        /// <param name="qIndex">The effective segment quantizer index.</param>
        /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
        /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        /// <param name="state">The candidate transform state.</param>
        /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
        public static abstract long EncodeCandidate(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            Span<TSample> reconstruction,
            ReadOnlySpan<TSample> above,
            ReadOnlySpan<TSample> left,
            bool hasLeft,
            bool hasAbove,
            Av1PredictionMode mode,
            int angleDelta,
            bool enableIntraEdgeFilter,
            bool smoothIntraEdges,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            Av1TransformType transformType,
            Av1Plane plane,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state);

        /// <summary>
        /// Builds one spatial intra prediction and its source residual for reuse across transform candidates.
        /// </summary>
        /// <param name="workspace">The reusable block workspace.</param>
        /// <param name="source">The coded source plane.</param>
        /// <param name="blockOrigin">The transform-block origin in plane samples.</param>
        /// <param name="prediction">The contiguous prediction destination.</param>
        /// <param name="above">The top reference samples, with prefix storage for the shared corner.</param>
        /// <param name="left">The left reference samples.</param>
        /// <param name="hasLeft">Whether the left reference is available.</param>
        /// <param name="hasAbove">Whether the top reference is available.</param>
        /// <param name="mode">The intra prediction mode.</param>
        /// <param name="angleDelta">The signed directional-angle adjustment.</param>
        /// <param name="enableIntraEdgeFilter">Whether sequence syntax enables directional edge filtering.</param>
        /// <param name="smoothIntraEdges">Whether a relevant neighboring block uses smooth prediction.</param>
        /// <param name="residual">The contiguous source-minus-prediction destination.</param>
        /// <param name="transformSize">The prediction dimensions.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        public static abstract void PrepareIntra(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            Span<TSample> prediction,
            ReadOnlySpan<TSample> above,
            ReadOnlySpan<TSample> left,
            bool hasLeft,
            bool hasAbove,
            Av1PredictionMode mode,
            int angleDelta,
            bool enableIntraEdgeFilter,
            bool smoothIntraEdges,
            Span<short> residual,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth);

        /// <summary>
        /// Builds one filter-intra prediction for reuse across transform candidates.
        /// </summary>
        /// <param name="workspace">The reusable block workspace.</param>
        /// <param name="source">The coded source plane.</param>
        /// <param name="blockOrigin">The transform-block origin in plane samples.</param>
        /// <param name="prediction">The contiguous prediction destination.</param>
        /// <param name="above">The top reference samples, with prefix storage for the shared corner.</param>
        /// <param name="left">The left reference samples.</param>
        /// <param name="residual">The contiguous source-minus-prediction destination.</param>
        /// <param name="filterIntraMode">The selected filter-intra mode.</param>
        /// <param name="transformSize">The prediction dimensions.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        public static abstract void PrepareFilterIntra(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            Span<TSample> prediction,
            ReadOnlySpan<TSample> above,
            ReadOnlySpan<TSample> left,
            Span<short> residual,
            Av1FilterIntraMode filterIntraMode,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth);

        /// <summary>
        /// Builds an intra-block-copy prediction and the matching source residual.
        /// </summary>
        /// <param name="source">The coded source plane.</param>
        /// <param name="blockOrigin">The destination block origin in plane samples.</param>
        /// <param name="reconstruction">The reconstructed plane containing the reference samples.</param>
        /// <param name="predictionOrigin">The integer reference origin preceding any half-sample phase.</param>
        /// <param name="halfX">Indicates whether the horizontal source phase is one half-sample.</param>
        /// <param name="halfY">Indicates whether the vertical source phase is one half-sample.</param>
        /// <param name="prediction">The contiguous prediction destination.</param>
        /// <param name="residual">The contiguous source-minus-prediction destination.</param>
        /// <param name="transformSize">The prediction dimensions.</param>
        public static abstract void PrepareIntraBlockCopyPrediction(
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            Buffer2DRegion<TSample> reconstruction,
            Point predictionOrigin,
            bool halfX,
            bool halfY,
            Span<TSample> prediction,
            Span<short> residual,
            Av1TransformSize transformSize);

        /// <summary>
        /// Subtracts a retained prediction from its source without rebuilding the inter predictor.
        /// </summary>
        /// <param name="source">The source plane.</param>
        /// <param name="blockOrigin">The block origin in plane samples.</param>
        /// <param name="prediction">The tightly packed prediction samples.</param>
        /// <param name="residual">The destination signed residual samples.</param>
        /// <param name="transformSize">The plane block geometry.</param>
        public static abstract void SubtractPrediction(
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            ReadOnlySpan<TSample> prediction,
            Span<short> residual,
            Av1TransformSize transformSize);

        /// <summary>
        /// Builds a translational prediction from a retained reference frame and the matching source residual.
        /// </summary>
        /// <param name="source">The coded source plane.</param>
        /// <param name="blockOrigin">The destination block origin in plane samples.</param>
        /// <param name="reference">The padded retained reference plane.</param>
        /// <param name="predictionOrigin">The integer reference origin preceding the subpixel phase.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="horizontalPhase">The horizontal phase in one-sixteenth-sample units.</param>
        /// <param name="verticalPhase">The vertical phase in one-sixteenth-sample units.</param>
        /// <param name="prediction">The contiguous prediction destination.</param>
        /// <param name="residual">The contiguous source-minus-prediction destination.</param>
        /// <param name="predictionScratch">The intermediate storage used by two-dimensional filtering.</param>
        /// <param name="transformSize">The prediction dimensions.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        public static abstract void PrepareTranslationalInterPrediction(
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            Buffer2DRegion<TSample> reference,
            Point predictionOrigin,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            Span<TSample> prediction,
            Span<short> residual,
            Span<short> predictionScratch,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth);

        /// <summary>
        /// Encodes one prepared prediction with the selected transform into decision scratch.
        /// </summary>
        /// <param name="workspace">The reusable block workspace.</param>
        /// <param name="source">The coded source plane.</param>
        /// <param name="blockOrigin">The transform-block origin in plane samples.</param>
        /// <param name="prediction">The contiguous prediction samples.</param>
        /// <param name="residual">The contiguous source-minus-prediction samples.</param>
        /// <param name="reconstruction">The candidate reconstruction.</param>
        /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
        /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
        /// <param name="transformSize">The transform dimensions.</param>
        /// <param name="transformType">The compound transform applied to the residual.</param>
        /// <param name="plane">The component plane containing the block.</param>
        /// <param name="qIndex">The effective segment quantizer index.</param>
        /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
        /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        /// <param name="state">The candidate transform state.</param>
        /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
        public static abstract long EncodePredictionCandidate(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            ReadOnlySpan<TSample> prediction,
            ReadOnlySpan<short> residual,
            Span<TSample> reconstruction,
            int reconstructionStride,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            Av1TransformType transformType,
            Av1Plane plane,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state);

        /// <summary>
        /// Encodes one chroma-from-luma candidate into contiguous decision scratch.
        /// </summary>
        /// <param name="workspace">The reusable block workspace.</param>
        /// <param name="source">The coded source plane.</param>
        /// <param name="blockOrigin">The transform-block origin in plane samples.</param>
        /// <param name="reconstruction">The contiguous candidate reconstruction.</param>
        /// <param name="dc">The cached DC predictor sample shared by every alpha.</param>
        /// <param name="lumaQ3">The zero-mean reconstructed-luma predictor surface.</param>
        /// <param name="alphaQ3">The signed chroma-from-luma multiplier.</param>
        /// <param name="quantizedCoefficients">The candidate entropy-coding coefficients.</param>
        /// <param name="transformSize">The transform dimensions.</param>
        /// <param name="plane">The component plane containing the block.</param>
        /// <param name="qIndex">The effective segment quantizer index.</param>
        /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
        /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        /// <param name="state">The candidate transform state.</param>
        /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
        public static abstract long EncodeChromaFromLumaCandidate(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            Span<TSample> reconstruction,
            TSample dc,
            ReadOnlySpan<short> lumaQ3,
            int alphaQ3,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            Av1Plane plane,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state);
    }

    private static int GetNormalizedVariance(int sum, int sumOfSquares, Av1BitDepth bitDepth)
    {
        int coefficientShift = bitDepth.GetBitCount() - 8;
        if (coefficientShift > 0)
        {
            // Normalize both moments before subtracting them so high-bit-depth motion search uses the
            // same eight-bit distortion scale as the encoder's other rate-distortion comparisons.
            int squareShift = coefficientShift * 2;
            sumOfSquares = (sumOfSquares + (1 << (squareShift - 1))) >> squareShift;
            sum = (sum + (1 << (coefficientShift - 1))) >> coefficientShift;
        }

        long variance = sumOfSquares - (((long)sum * sum) / 64);
        return (int)Math.Max(variance, 0);
    }

    /// <summary>
    /// Encodes blocks stored as eight-bit samples.
    /// </summary>
    internal readonly struct ByteOperator : IBlockEncodingOperator<byte>
    {
        /// <inheritdoc/>
        public static void PreparePrediction(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> reference,
            int referenceStride,
            int referenceOrigin,
            Span<byte> prediction,
            Span<short> residual,
            Span<short> scratch,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            int bitDepth)
            => Av1MotionSearchBase.ByteOperator.PreparePrediction(
                source,
                sourceStride,
                reference,
                referenceStride,
                referenceOrigin,
                prediction,
                residual,
                scratch,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                bitDepth);

        /// <inheritdoc/>
        public static void Predict(
            ReadOnlySpan<byte> reference,
            int referenceStride,
            int referenceOrigin,
            Span<byte> buffer,
            int width,
            int height,
            int horizontalPhase,
            int verticalPhase,
            int taps,
            int bitDepth)
            => Av1MotionSearchBase.ByteOperator.Predict(
                reference, referenceStride, referenceOrigin, buffer, width, height, horizontalPhase, verticalPhase, taps, bitDepth);

        /// <inheritdoc/>
        public static int SumAbsoluteDifferences(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            int width,
            int height,
            int rowStep)
            => Av1MotionSearchBase.ByteOperator.SumAbsoluteDifferences(
                source, sourceStride, prediction, predictionStride, width, height, rowStep);

        /// <inheritdoc/>
        public static void GetMoments(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            int width,
            int height,
            out int sum,
            out long squares)
            => Av1MotionSearchBase.ByteOperator.GetMoments(
                source, sourceStride, prediction, predictionStride, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static Span<byte> GetLeftReference(Span<short> residual, int length)
            => MemoryMarshal.AsBytes(residual)[..length];

        /// <inheritdoc/>
        public static byte CreateSample(int value) => (byte)value;

        /// <inheritdoc/>
        public static uint GetHashSample(byte sample) => sample;

        /// <inheritdoc/>
        public static bool BlocksEqual(Buffer2DRegion<byte> plane, Point first, Point second)
        {
            for (int row = 0; row < 8; row++)
            {
                ReadOnlySpan<byte> firstRow = plane.DangerousGetRowSpan(first.Y + row)[first.X..];
                ReadOnlySpan<byte> secondRow = plane.DangerousGetRowSpan(second.Y + row)[second.X..];

                // Compare the complete row as byte lanes so collision rejection remains independent of native endianness.
                if (Vector64.Create(firstRow) != Vector64.Create(secondRow))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public static int GetSumOfAbsoluteDifferences(
            Buffer2DRegion<byte> source,
            Point sourceOrigin,
            Buffer2DRegion<byte> reconstruction,
            Point predictionOrigin)
            => Av1ResidualBuilder.SumAbsoluteDifferences8x8(
                Av1TransformBlockEncoder.GetPlaneSpan(source, sourceOrigin),
                source.Stride,
                Av1TransformBlockEncoder.GetPlaneSpan(reconstruction, predictionOrigin),
                reconstruction.Stride);

        /// <inheritdoc/>
        public static void GetFourSumsOfAbsoluteDifferences(
            Buffer2DRegion<byte> source,
            Point sourceOrigin,
            Buffer2DRegion<byte> reconstruction,
            Point firstPredictionOrigin,
            Span<int> sums)
            => Av1ResidualBuilder.SumFourAbsoluteDifferences8x8(
                Av1TransformBlockEncoder.GetPlaneSpan(source, sourceOrigin),
                source.Stride,
                Av1TransformBlockEncoder.GetPlaneSpan(reconstruction, firstPredictionOrigin),
                reconstruction.Stride,
                sums);

        /// <inheritdoc/>
        public static int GetVariance(
            Buffer2DRegion<byte> source,
            Point sourceOrigin,
            Buffer2DRegion<byte> reconstruction,
            Point predictionOrigin,
            Av1BitDepth bitDepth)
        {
            Av1ResidualBuilder.GetMoments8x8(
                Av1TransformBlockEncoder.GetPlaneSpan(source, sourceOrigin),
                source.Stride,
                Av1TransformBlockEncoder.GetPlaneSpan(reconstruction, predictionOrigin),
                reconstruction.Stride,
                out int sum,
                out int sumOfSquares);

            return GetNormalizedVariance(sum, sumOfSquares, bitDepth);
        }

        /// <inheritdoc/>
        public static void CopyPaletteSamples(
            Buffer2DRegion<byte> source,
            Point blockOrigin,
            int rows,
            int columns,
            Span<short> samples)
        {
            int sampleOffset = 0;
            for (int row = 0; row < rows; row++)
            {
                ReadOnlySpan<byte> sourceRow = source
                    .DangerousGetRowSpan(blockOrigin.Y + row)
                    .Slice(blockOrigin.X, columns);

                // A complete row widens in one vector; clipped edge rows retain scalar bounds.
                if (columns == 8 && Vector128.IsHardwareAccelerated)
                {
                    Vector128.WidenLower(Vector128.Create(Vector64.Create(sourceRow), Vector64<byte>.Zero)).AsInt16().CopyTo(samples[sampleOffset..]);

                    sampleOffset += columns;
                    continue;
                }

                for (int column = 0; column < sourceRow.Length; column++)
                {
                    samples[sampleOffset++] = sourceRow[column];
                }
            }
        }

        /// <inheritdoc/>
        public static void PreparePalette(
            Buffer2DRegion<byte> source,
            Point blockOrigin,
            ReadOnlySpan<ushort> paletteColors,
            Buffer2DRegion<byte> colorIndexMap,
            Span<byte> prediction,
            Span<short> residual,
            Av1TransformSize transformSize)
        {
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            Av1PalettePredictor.Predict(
                paletteColors,
                colorIndexMap,
                prediction,
                width,
                width,
                height);

            Av1ResidualBuilder.Subtract(
                Av1TransformBlockEncoder.GetPlaneSpan(source, blockOrigin),
                source.Stride,
                prediction,
                width,
                residual,
                width,
                width,
                height);
        }

        /// <inheritdoc/>
        public static void PrepareChromaFromLuma(
            Buffer2DRegion<byte> reconstruction,
            Point blockOrigin,
            Span<short> lumaQ3,
            Av1TransformSize transformSize,
            bool subsamplingX,
            bool subsamplingY)
            => Av1ChromaFromLumaContext.PrepareBlock(
                Av1TransformBlockEncoder.GetPlaneSpan(reconstruction, blockOrigin),
                reconstruction.Stride,
                lumaQ3,
                transformSize,
                subsamplingX,
                subsamplingY);

        /// <inheritdoc/>
        public static void PrepareChromaFromLumaDc(
            Span<byte> reconstruction,
            ReadOnlySpan<byte> above,
            ReadOnlySpan<byte> left,
            bool hasLeft,
            bool hasAbove,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth)
        {
            int width = transformSize.GetWidth();
            Av1DcIntraPredictor.Predict(
                hasLeft,
                hasAbove,
                reconstruction,
                width,
                above,
                left,
                width,
                transformSize.GetHeight());
        }

        /// <inheritdoc/>
        public static void Encode(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<byte> source,
            Buffer2DRegion<byte> reconstruction,
            Point blockOrigin,
            ReadOnlySpan<byte> above,
            ReadOnlySpan<byte> left,
            bool hasLeft,
            bool hasAbove,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1Plane plane,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.EncodeIntraDcLossy(
                workspace,
                source,
                reconstruction,
                blockOrigin,
                above,
                left,
                hasLeft,
                hasAbove,
                quantizedCoefficients,
                transformSize,
                Av1TransformType.DctDct,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                plane,
                ref state);

        /// <inheritdoc/>
        public static long EncodeCandidate(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<byte> source,
            Point blockOrigin,
            Span<byte> reconstruction,
            ReadOnlySpan<byte> above,
            ReadOnlySpan<byte> left,
            bool hasLeft,
            bool hasAbove,
            Av1PredictionMode mode,
            int angleDelta,
            bool enableIntraEdgeFilter,
            bool smoothIntraEdges,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            Av1TransformType transformType,
            Av1Plane plane,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.EncodeIntraLossyCandidate(
                workspace,
                source,
                blockOrigin,
                reconstruction,
                above,
                left,
                hasLeft,
                hasAbove,
                mode,
                angleDelta,
                enableIntraEdgeFilter,
                smoothIntraEdges,
                quantizedCoefficients,
                transformSize,
                transformType,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                plane,
                ref state);

        /// <inheritdoc/>
        public static void PrepareIntra(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<byte> source,
            Point blockOrigin,
            Span<byte> prediction,
            ReadOnlySpan<byte> above,
            ReadOnlySpan<byte> left,
            bool hasLeft,
            bool hasAbove,
            Av1PredictionMode mode,
            int angleDelta,
            bool enableIntraEdgeFilter,
            bool smoothIntraEdges,
            Span<short> residual,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth)
            => Av1TransformBlockEncoder.PrepareIntraPrediction(
                workspace,
                Av1TransformBlockEncoder.GetPlaneSpan(source, blockOrigin),
                source.Stride,
                prediction,
                transformSize.GetWidth(),
                above,
                left,
                hasLeft,
                hasAbove,
                mode,
                angleDelta,
                enableIntraEdgeFilter,
                smoothIntraEdges,
                residual,
                transformSize);

        /// <inheritdoc/>
        public static void PrepareFilterIntra(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<byte> source,
            Point blockOrigin,
            Span<byte> prediction,
            ReadOnlySpan<byte> above,
            ReadOnlySpan<byte> left,
            Span<short> residual,
            Av1FilterIntraMode filterIntraMode,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth)
        {
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();

            // Prediction finishes before transform search, so its temporary rows can borrow the transform workspace.
            Span<byte> filterScratch = MemoryMarshal.AsBytes(workspace.TransformWorkspace).Slice(
                0,
                Av1FilterIntraPredictorBase.ScratchLength);

            Av1FilterIntraPredictorBase.GetPredictor(filterIntraMode)
                .Predict(prediction, width, above, left, width, height, filterScratch);

            Av1ResidualBuilder.Subtract(
                Av1TransformBlockEncoder.GetPlaneSpan(source, blockOrigin),
                source.Stride,
                prediction,
                width,
                residual,
                width,
                width,
                height);
        }

        /// <inheritdoc/>
        public static void PrepareIntraBlockCopyPrediction(
            Buffer2DRegion<byte> source,
            Point blockOrigin,
            Buffer2DRegion<byte> reconstruction,
            Point predictionOrigin,
            bool halfX,
            bool halfY,
            Span<byte> prediction,
            Span<short> residual,
            Av1TransformSize transformSize)
        {
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            Av1IntraBlockCopyPredictor.Predict(
                Av1TransformBlockEncoder.GetPlaneSpan(reconstruction, predictionOrigin),
                reconstruction.Stride,
                prediction,
                width,
                width,
                height,
                halfX,
                halfY);

            Av1ResidualBuilder.Subtract(
                Av1TransformBlockEncoder.GetPlaneSpan(source, blockOrigin),
                source.Stride,
                prediction,
                width,
                residual,
                width,
                width,
                height);
        }

        /// <inheritdoc/>
        public static void SubtractPrediction(
            Buffer2DRegion<byte> source,
            Point blockOrigin,
            ReadOnlySpan<byte> prediction,
            Span<short> residual,
            Av1TransformSize transformSize)
            => Av1ResidualBuilder.Subtract(
                Av1TransformBlockEncoder.GetPlaneSpan(source, blockOrigin),
                source.Stride,
                prediction,
                transformSize.GetWidth(),
                residual,
                transformSize.GetWidth(),
                transformSize.GetWidth(),
                transformSize.GetHeight());

        /// <inheritdoc/>
        public static void PrepareTranslationalInterPrediction(
            Buffer2DRegion<byte> source,
            Point blockOrigin,
            Buffer2DRegion<byte> reference,
            Point predictionOrigin,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            Span<byte> prediction,
            Span<short> residual,
            Span<short> predictionScratch,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth)
        {
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            Rectangle referenceBounds = reference.Bounds;
            int referenceOrigin =
                ((referenceBounds.Y + predictionOrigin.Y) * reference.Stride) +
                referenceBounds.X +
                predictionOrigin.X;

            Av1TranslationalInterPredictor.Predict(
                reference.Buffer.DangerousGetSingleSpan(),
                reference.Stride,
                referenceOrigin,
                prediction,
                width,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                predictionScratch);

            SubtractPrediction(source, blockOrigin, prediction, residual, transformSize);
        }

        /// <inheritdoc/>
        public static long EncodePredictionCandidate(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<byte> source,
            Point blockOrigin,
            ReadOnlySpan<byte> prediction,
            ReadOnlySpan<short> residual,
            Span<byte> reconstruction,
            int reconstructionStride,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            Av1TransformType transformType,
            Av1Plane plane,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.EncodePredictionLossyCandidate(
                workspace,
                source,
                blockOrigin,
                prediction,
                residual,
                reconstruction,
                reconstructionStride,
                quantizedCoefficients,
                transformSize,
                transformType,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                plane,
                ref state);

        /// <inheritdoc/>
        public static long EncodeChromaFromLumaCandidate(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<byte> source,
            Point blockOrigin,
            Span<byte> reconstruction,
            byte dc,
            ReadOnlySpan<short> lumaQ3,
            int alphaQ3,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            Av1Plane plane,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.EncodeChromaFromLumaLossyCandidate(
                workspace,
                source,
                blockOrigin,
                reconstruction,
                dc,
                lumaQ3,
                alphaQ3,
                quantizedCoefficients,
                transformSize,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                plane,
                ref state);
    }

    /// <summary>
    /// Encodes blocks stored as high-bit-depth samples.
    /// </summary>
    internal readonly struct UInt16Operator : IBlockEncodingOperator<ushort>
    {
        /// <inheritdoc/>
        public static void PreparePrediction(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> reference,
            int referenceStride,
            int referenceOrigin,
            Span<ushort> prediction,
            Span<short> residual,
            Span<short> scratch,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            int bitDepth)
            => Av1MotionSearchBase.UInt16Operator.PreparePrediction(
                source,
                sourceStride,
                reference,
                referenceStride,
                referenceOrigin,
                prediction,
                residual,
                scratch,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                bitDepth);

        /// <inheritdoc/>
        public static void Predict(
            ReadOnlySpan<ushort> reference,
            int referenceStride,
            int referenceOrigin,
            Span<ushort> buffer,
            int width,
            int height,
            int horizontalPhase,
            int verticalPhase,
            int taps,
            int bitDepth)
            => Av1MotionSearchBase.UInt16Operator.Predict(
                reference, referenceStride, referenceOrigin, buffer, width, height, horizontalPhase, verticalPhase, taps, bitDepth);

        /// <inheritdoc/>
        public static int SumAbsoluteDifferences(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            int width,
            int height,
            int rowStep)
            => Av1MotionSearchBase.UInt16Operator.SumAbsoluteDifferences(
                source, sourceStride, prediction, predictionStride, width, height, rowStep);

        /// <inheritdoc/>
        public static void GetMoments(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            int width,
            int height,
            out int sum,
            out long squares)
            => Av1MotionSearchBase.UInt16Operator.GetMoments(
                source, sourceStride, prediction, predictionStride, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static Span<ushort> GetLeftReference(Span<short> residual, int length)
            => MemoryMarshal.Cast<short, ushort>(residual)[..length];

        /// <inheritdoc/>
        public static ushort CreateSample(int value) => (ushort)value;

        /// <inheritdoc/>
        public static uint GetHashSample(ushort sample) => sample;

        /// <inheritdoc/>
        public static bool BlocksEqual(Buffer2DRegion<ushort> plane, Point first, Point second)
        {
            for (int row = 0; row < 8; row++)
            {
                ReadOnlySpan<ushort> firstRow = plane.DangerousGetRowSpan(first.Y + row)[first.X..];
                ReadOnlySpan<ushort> secondRow = plane.DangerousGetRowSpan(second.Y + row)[second.X..];
                if (Vector128.IsHardwareAccelerated)
                {
                    Vector128<ushort> firstSamples = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(firstRow));
                    Vector128<ushort> secondSamples = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(secondRow));
                    if (!Vector128.EqualsAll(firstSamples, secondSamples))
                    {
                        return false;
                    }
                }
                else if (!firstRow[..8].SequenceEqual(secondRow[..8]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public static int GetSumOfAbsoluteDifferences(
            Buffer2DRegion<ushort> source,
            Point sourceOrigin,
            Buffer2DRegion<ushort> reconstruction,
            Point predictionOrigin)
            => Av1ResidualBuilder.SumAbsoluteDifferences8x8(
                Av1TransformBlockEncoder.GetPlaneSpan(source, sourceOrigin),
                source.Stride,
                Av1TransformBlockEncoder.GetPlaneSpan(reconstruction, predictionOrigin),
                reconstruction.Stride);

        /// <inheritdoc/>
        public static void GetFourSumsOfAbsoluteDifferences(
            Buffer2DRegion<ushort> source,
            Point sourceOrigin,
            Buffer2DRegion<ushort> reconstruction,
            Point firstPredictionOrigin,
            Span<int> sums)
            => Av1ResidualBuilder.SumFourAbsoluteDifferences8x8(
                Av1TransformBlockEncoder.GetPlaneSpan(source, sourceOrigin),
                source.Stride,
                Av1TransformBlockEncoder.GetPlaneSpan(reconstruction, firstPredictionOrigin),
                reconstruction.Stride,
                sums);

        /// <inheritdoc/>
        public static int GetVariance(
            Buffer2DRegion<ushort> source,
            Point sourceOrigin,
            Buffer2DRegion<ushort> reconstruction,
            Point predictionOrigin,
            Av1BitDepth bitDepth)
        {
            Av1ResidualBuilder.GetMoments8x8(
                Av1TransformBlockEncoder.GetPlaneSpan(source, sourceOrigin),
                source.Stride,
                Av1TransformBlockEncoder.GetPlaneSpan(reconstruction, predictionOrigin),
                reconstruction.Stride,
                out int sum,
                out int sumOfSquares);

            return GetNormalizedVariance(sum, sumOfSquares, bitDepth);
        }

        /// <inheritdoc/>
        public static void CopyPaletteSamples(
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
            int rows,
            int columns,
            Span<short> samples)
        {
            int sampleOffset = 0;
            for (int row = 0; row < rows; row++)
            {
                ReadOnlySpan<ushort> sourceRow = source
                    .DangerousGetRowSpan(blockOrigin.Y + row)
                    .Slice(blockOrigin.X, columns);

                MemoryMarshal.Cast<ushort, short>(sourceRow).CopyTo(samples[sampleOffset..]);
                sampleOffset += sourceRow.Length;
            }
        }

        /// <inheritdoc/>
        public static void PreparePalette(
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
            ReadOnlySpan<ushort> paletteColors,
            Buffer2DRegion<byte> colorIndexMap,
            Span<ushort> prediction,
            Span<short> residual,
            Av1TransformSize transformSize)
        {
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            Av1PalettePredictor.Predict(
                paletteColors,
                colorIndexMap,
                MemoryMarshal.Cast<ushort, short>(prediction),
                width,
                width,
                height);

            Av1ResidualBuilder.Subtract(
                Av1TransformBlockEncoder.GetPlaneSpan(source, blockOrigin),
                source.Stride,
                prediction,
                width,
                residual,
                width,
                width,
                height);
        }

        /// <inheritdoc/>
        public static void PrepareChromaFromLuma(
            Buffer2DRegion<ushort> reconstruction,
            Point blockOrigin,
            Span<short> lumaQ3,
            Av1TransformSize transformSize,
            bool subsamplingX,
            bool subsamplingY)
            => Av1ChromaFromLumaContext.PrepareBlock(
                MemoryMarshal.Cast<ushort, short>(Av1TransformBlockEncoder.GetPlaneSpan(reconstruction, blockOrigin)),
                reconstruction.Stride,
                lumaQ3,
                transformSize,
                subsamplingX,
                subsamplingY);

        /// <inheritdoc/>
        public static void PrepareChromaFromLumaDc(
            Span<ushort> reconstruction,
            ReadOnlySpan<ushort> above,
            ReadOnlySpan<ushort> left,
            bool hasLeft,
            bool hasAbove,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth)
        {
            int width = transformSize.GetWidth();
            Av1DcIntraPredictor.Predict(
                hasLeft,
                hasAbove,
                MemoryMarshal.Cast<ushort, short>(reconstruction),
                width,
                MemoryMarshal.Cast<ushort, short>(above),
                MemoryMarshal.Cast<ushort, short>(left),
                width,
                transformSize.GetHeight(),
                bitDepth.GetBitCount());
        }

        /// <inheritdoc/>
        public static void Encode(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<ushort> source,
            Buffer2DRegion<ushort> reconstruction,
            Point blockOrigin,
            ReadOnlySpan<ushort> above,
            ReadOnlySpan<ushort> left,
            bool hasLeft,
            bool hasAbove,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1Plane plane,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.EncodeIntraDcLossy(
                workspace,
                source,
                reconstruction,
                blockOrigin,
                above,
                left,
                hasLeft,
                hasAbove,
                quantizedCoefficients,
                transformSize,
                Av1TransformType.DctDct,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                plane,
                bitDepth,
                ref state);

        /// <inheritdoc/>
        public static long EncodeCandidate(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
            Span<ushort> reconstruction,
            ReadOnlySpan<ushort> above,
            ReadOnlySpan<ushort> left,
            bool hasLeft,
            bool hasAbove,
            Av1PredictionMode mode,
            int angleDelta,
            bool enableIntraEdgeFilter,
            bool smoothIntraEdges,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            Av1TransformType transformType,
            Av1Plane plane,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.EncodeIntraLossyCandidate(
                workspace,
                source,
                blockOrigin,
                reconstruction,
                above,
                left,
                hasLeft,
                hasAbove,
                mode,
                angleDelta,
                enableIntraEdgeFilter,
                smoothIntraEdges,
                quantizedCoefficients,
                transformSize,
                transformType,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                plane,
                bitDepth,
                ref state);

        /// <inheritdoc/>
        public static void PrepareIntra(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
            Span<ushort> prediction,
            ReadOnlySpan<ushort> above,
            ReadOnlySpan<ushort> left,
            bool hasLeft,
            bool hasAbove,
            Av1PredictionMode mode,
            int angleDelta,
            bool enableIntraEdgeFilter,
            bool smoothIntraEdges,
            Span<short> residual,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth)
            => Av1TransformBlockEncoder.PrepareIntraPrediction(
                workspace,
                Av1TransformBlockEncoder.GetPlaneSpan(source, blockOrigin),
                source.Stride,
                prediction,
                transformSize.GetWidth(),
                above,
                left,
                hasLeft,
                hasAbove,
                mode,
                angleDelta,
                enableIntraEdgeFilter,
                smoothIntraEdges,
                residual,
                transformSize,
                bitDepth);

        /// <inheritdoc/>
        public static void PrepareFilterIntra(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
            Span<ushort> prediction,
            ReadOnlySpan<ushort> above,
            ReadOnlySpan<ushort> left,
            Span<short> residual,
            Av1FilterIntraMode filterIntraMode,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth)
        {
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();

            // Prediction finishes before transform search, so its temporary rows can borrow the transform workspace.
            Span<short> filterScratch = MemoryMarshal.Cast<int, short>(workspace.TransformWorkspace).Slice(
                0,
                Av1FilterIntraPredictorBase.ScratchLength);

            Av1FilterIntraPredictorBase.GetPredictor(filterIntraMode)
                .Predict(
                    MemoryMarshal.Cast<ushort, short>(prediction),
                    width,
                    MemoryMarshal.Cast<ushort, short>(above),
                    MemoryMarshal.Cast<ushort, short>(left),
                    width,
                    height,
                    bitDepth.GetBitCount(),
                    filterScratch);

            Av1ResidualBuilder.Subtract(
                Av1TransformBlockEncoder.GetPlaneSpan(source, blockOrigin),
                source.Stride,
                prediction,
                width,
                residual,
                width,
                width,
                height);
        }

        /// <inheritdoc/>
        public static void PrepareIntraBlockCopyPrediction(
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
            Buffer2DRegion<ushort> reconstruction,
            Point predictionOrigin,
            bool halfX,
            bool halfY,
            Span<ushort> prediction,
            Span<short> residual,
            Av1TransformSize transformSize)
        {
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            Av1IntraBlockCopyPredictor.Predict(
                MemoryMarshal.Cast<ushort, short>(Av1TransformBlockEncoder.GetPlaneSpan(reconstruction, predictionOrigin)),
                reconstruction.Stride,
                MemoryMarshal.Cast<ushort, short>(prediction),
                width,
                width,
                height,
                halfX,
                halfY);

            Av1ResidualBuilder.Subtract(
                Av1TransformBlockEncoder.GetPlaneSpan(source, blockOrigin),
                source.Stride,
                prediction,
                width,
                residual,
                width,
                width,
                height);
        }

        /// <inheritdoc/>
        public static void SubtractPrediction(
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
            ReadOnlySpan<ushort> prediction,
            Span<short> residual,
            Av1TransformSize transformSize)
            => Av1ResidualBuilder.Subtract(
                Av1TransformBlockEncoder.GetPlaneSpan(source, blockOrigin),
                source.Stride,
                prediction,
                transformSize.GetWidth(),
                residual,
                transformSize.GetWidth(),
                transformSize.GetWidth(),
                transformSize.GetHeight());

        /// <inheritdoc/>
        public static void PrepareTranslationalInterPrediction(
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
            Buffer2DRegion<ushort> reference,
            Point predictionOrigin,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            Span<ushort> prediction,
            Span<short> residual,
            Span<short> predictionScratch,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth)
        {
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            Rectangle referenceBounds = reference.Bounds;
            int referenceOrigin =
                ((referenceBounds.Y + predictionOrigin.Y) * reference.Stride) +
                referenceBounds.X +
                predictionOrigin.X;

            Av1TranslationalInterPredictor.Predict(
                reference.Buffer.DangerousGetSingleSpan(),
                reference.Stride,
                referenceOrigin,
                prediction,
                width,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                bitDepth.GetBitCount(),
                predictionScratch);

            SubtractPrediction(source, blockOrigin, prediction, residual, transformSize);
        }

        /// <inheritdoc/>
        public static long EncodePredictionCandidate(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
            ReadOnlySpan<ushort> prediction,
            ReadOnlySpan<short> residual,
            Span<ushort> reconstruction,
            int reconstructionStride,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            Av1TransformType transformType,
            Av1Plane plane,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.EncodePredictionLossyCandidate(
                workspace,
                source,
                blockOrigin,
                prediction,
                residual,
                reconstruction,
                reconstructionStride,
                quantizedCoefficients,
                transformSize,
                transformType,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                plane,
                bitDepth,
                ref state);

        /// <inheritdoc/>
        public static long EncodeChromaFromLumaCandidate(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
            Span<ushort> reconstruction,
            ushort dc,
            ReadOnlySpan<short> lumaQ3,
            int alphaQ3,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            Av1Plane plane,
            int qIndex,
            int dcDeltaQ,
            int acDeltaQ,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.EncodeChromaFromLumaLossyCandidate(
                workspace,
                source,
                blockOrigin,
                reconstruction,
                dc,
                lumaQ3,
                alphaQ3,
                quantizedCoefficients,
                transformSize,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                plane,
                bitDepth,
                ref state);
    }
}
