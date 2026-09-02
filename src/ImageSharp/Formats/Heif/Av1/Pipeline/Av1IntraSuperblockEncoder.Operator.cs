// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
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
    internal interface IBlockEncodingOperator<TSample> : Av1IntraBlockCopySearchIndex.ISearchOperation<TSample>
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
        /// Encodes one prepared prediction with the selected transform into decision scratch.
        /// </summary>
        /// <param name="workspace">The reusable block workspace.</param>
        /// <param name="source">The coded source plane.</param>
        /// <param name="blockOrigin">The transform-block origin in plane samples.</param>
        /// <param name="prediction">The contiguous prediction samples.</param>
        /// <param name="residual">The contiguous source-minus-prediction samples.</param>
        /// <param name="reconstruction">The contiguous candidate reconstruction.</param>
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

                // An 8x8 search row occupies one machine word, so one unaligned load and comparison replaces
                // eight dependent scalar branches while retaining exact collision rejection.
                if (MemoryMarshal.Read<ulong>(firstRow) != MemoryMarshal.Read<ulong>(secondRow))
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
        {
            int sum = 0;
            if (Vector128.IsHardwareAccelerated)
            {
                for (int row = 0; row < 8; row++)
                {
                    ReadOnlySpan<byte> sourceRow = source.DangerousGetRowSpan(sourceOrigin.Y + row)[sourceOrigin.X..];
                    ReadOnlySpan<byte> predictionRow =
                        reconstruction.DangerousGetRowSpan(predictionOrigin.Y + row)[predictionOrigin.X..];

                    Vector128<short> difference =
                        (Vector128.WidenLower(Vector128.CreateScalarUnsafe(MemoryMarshal.Read<ulong>(sourceRow)).AsByte()) -
                         Vector128.WidenLower(Vector128.CreateScalarUnsafe(MemoryMarshal.Read<ulong>(predictionRow)).AsByte()))
                        .AsInt16();

                    // Widened signed differences retain both subtraction directions; absolute values then reduce
                    // the complete eight-sample row without scalar extraction or per-sample branches.
                    sum += Vector128.Sum(Vector128.Abs(difference));
                }
            }
            else
            {
                for (int row = 0; row < 8; row++)
                {
                    ReadOnlySpan<byte> sourceRow = source.DangerousGetRowSpan(sourceOrigin.Y + row)[sourceOrigin.X..];
                    ReadOnlySpan<byte> predictionRow =
                        reconstruction.DangerousGetRowSpan(predictionOrigin.Y + row)[predictionOrigin.X..];

                    for (int column = 0; column < 8; column++)
                    {
                        sum += Math.Abs(sourceRow[column] - predictionRow[column]);
                    }
                }
            }

            return sum;
        }

        /// <inheritdoc/>
        public static int GetVariance(
            Buffer2DRegion<byte> source,
            Point sourceOrigin,
            Buffer2DRegion<byte> reconstruction,
            Point predictionOrigin,
            Av1BitDepth bitDepth)
        {
            int sum = 0;
            int sumOfSquares = 0;
            if (Vector128.IsHardwareAccelerated)
            {
                for (int row = 0; row < 8; row++)
                {
                    ReadOnlySpan<byte> sourceRow = source.DangerousGetRowSpan(sourceOrigin.Y + row)[sourceOrigin.X..];
                    ReadOnlySpan<byte> predictionRow =
                        reconstruction.DangerousGetRowSpan(predictionOrigin.Y + row)[predictionOrigin.X..];

                    Vector128<short> difference =
                        (Vector128.WidenLower(Vector128.CreateScalarUnsafe(MemoryMarshal.Read<ulong>(sourceRow)).AsByte()) -
                         Vector128.WidenLower(Vector128.CreateScalarUnsafe(MemoryMarshal.Read<ulong>(predictionRow)).AsByte()))
                        .AsInt16();

                    // Widen before squaring so signed residuals cannot wrap in 16-bit lanes.
                    Vector128<int> lower = Vector128.WidenLower(difference);
                    Vector128<int> upper = Vector128.WidenUpper(difference);
                    sum += Vector128.Sum(difference);
                    sumOfSquares += Vector128.Sum(lower * lower) + Vector128.Sum(upper * upper);
                }
            }
            else
            {
                for (int row = 0; row < 8; row++)
                {
                    ReadOnlySpan<byte> sourceRow = source.DangerousGetRowSpan(sourceOrigin.Y + row)[sourceOrigin.X..];
                    ReadOnlySpan<byte> predictionRow =
                        reconstruction.DangerousGetRowSpan(predictionOrigin.Y + row)[predictionOrigin.X..];

                    for (int column = 0; column < 8; column++)
                    {
                        int difference = sourceRow[column] - predictionRow[column];
                        sum += difference;
                        sumOfSquares += difference * difference;
                    }
                }
            }

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
                    ulong packed = MemoryMarshal.Read<ulong>(sourceRow);
                    Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte())
                        .AsInt16()
                        .CopyTo(samples[sampleOffset..]);

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
                quantizedCoefficients,
                transformSize,
                transformType,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                plane,
                ref state);

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
        public static long EncodePredictionCandidate(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<byte> source,
            Point blockOrigin,
            ReadOnlySpan<byte> prediction,
            ReadOnlySpan<short> residual,
            Span<byte> reconstruction,
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
        {
            int sum = 0;
            if (Vector128.IsHardwareAccelerated)
            {
                for (int row = 0; row < 8; row++)
                {
                    ReadOnlySpan<ushort> sourceRow = source.DangerousGetRowSpan(sourceOrigin.Y + row)[sourceOrigin.X..];
                    ReadOnlySpan<ushort> predictionRow =
                        reconstruction.DangerousGetRowSpan(predictionOrigin.Y + row)[predictionOrigin.X..];

                    // Twelve-bit samples remain within signed 16-bit subtraction and absolute-value ranges,
                    // allowing all eight row differences to stay packed until their horizontal reduction.
                    Vector128<short> difference =
                        (Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(sourceRow)) -
                         Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(predictionRow)))
                        .AsInt16();

                    sum += Vector128.Sum(Vector128.Abs(difference));
                }
            }
            else
            {
                for (int row = 0; row < 8; row++)
                {
                    ReadOnlySpan<ushort> sourceRow = source.DangerousGetRowSpan(sourceOrigin.Y + row)[sourceOrigin.X..];
                    ReadOnlySpan<ushort> predictionRow =
                        reconstruction.DangerousGetRowSpan(predictionOrigin.Y + row)[predictionOrigin.X..];

                    for (int column = 0; column < 8; column++)
                    {
                        sum += Math.Abs(sourceRow[column] - predictionRow[column]);
                    }
                }
            }

            return sum;
        }

        /// <inheritdoc/>
        public static int GetVariance(
            Buffer2DRegion<ushort> source,
            Point sourceOrigin,
            Buffer2DRegion<ushort> reconstruction,
            Point predictionOrigin,
            Av1BitDepth bitDepth)
        {
            int sum = 0;
            int sumOfSquares = 0;
            if (Vector128.IsHardwareAccelerated)
            {
                for (int row = 0; row < 8; row++)
                {
                    ReadOnlySpan<ushort> sourceRow = source.DangerousGetRowSpan(sourceOrigin.Y + row)[sourceOrigin.X..];
                    ReadOnlySpan<ushort> predictionRow =
                        reconstruction.DangerousGetRowSpan(predictionOrigin.Y + row)[predictionOrigin.X..];

                    // AV1's high-bit-depth domain tops out at 4095, so signed 16-bit subtraction preserves
                    // every possible sample difference before the square is widened to 32-bit lanes.
                    Vector128<short> difference =
                        (Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(sourceRow)) -
                         Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(predictionRow)))
                        .AsInt16();

                    Vector128<int> lower = Vector128.WidenLower(difference);
                    Vector128<int> upper = Vector128.WidenUpper(difference);
                    sum += Vector128.Sum(difference);
                    sumOfSquares += Vector128.Sum(lower * lower) + Vector128.Sum(upper * upper);
                }
            }
            else
            {
                for (int row = 0; row < 8; row++)
                {
                    ReadOnlySpan<ushort> sourceRow = source.DangerousGetRowSpan(sourceOrigin.Y + row)[sourceOrigin.X..];
                    ReadOnlySpan<ushort> predictionRow =
                        reconstruction.DangerousGetRowSpan(predictionOrigin.Y + row)[predictionOrigin.X..];

                    for (int column = 0; column < 8; column++)
                    {
                        int difference = sourceRow[column] - predictionRow[column];
                        sum += difference;
                        sumOfSquares += difference * difference;
                    }
                }
            }

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
        public static long EncodePredictionCandidate(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
            ReadOnlySpan<ushort> prediction,
            ReadOnlySpan<short> residual,
            Span<ushort> reconstruction,
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
