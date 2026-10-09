// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
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
        /// Converts a valid sample value to the native plane storage type.
        /// </summary>
        /// <param name="value">The sample value.</param>
        /// <returns>The converted sample.</returns>
        public static abstract TSample CreateSample(int value);

        /// <summary>
        /// Converts a native unsigned sample to an integer for source-domain analysis.
        /// </summary>
        /// <param name="sample">The source sample.</param>
        /// <returns>The sample value.</returns>
        public static abstract int GetSampleValue(TSample sample);

        /// <summary>
        /// Measures how far a block departs from its own 3x3 smoothing. The columns and rows past the visible ones repeat the last visible
        /// column and row.
        /// </summary>
        /// <param name="source">The samples, starting at the block origin.</param>
        /// <param name="stride">The row stride.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="visibleWidth">The number of columns read from the samples, at least one.</param>
        /// <param name="visibleHeight">The number of rows read from the samples, at least one.</param>
        /// <returns>The measure, before any bit-depth scaling.</returns>
        public static abstract long GetVarianceStatistic(
            ReadOnlySpan<TSample> source,
            int stride,
            int width,
            int height,
            int visibleWidth,
            int visibleHeight);

        /// <summary>
        /// Gets the rounded average of a four-by-four source block.
        /// </summary>
        /// <param name="source">The source block, from its top-left sample.</param>
        /// <param name="stride">The number of samples between rows of <paramref name="source"/>.</param>
        /// <returns>The average in the source sample precision.</returns>
        public static abstract int GetAverage4x4(ReadOnlySpan<TSample> source, int stride);

        /// <summary>
        /// Computes the rounded mean of an eight-by-eight sample block.
        /// </summary>
        /// <param name="source">The samples beginning at the block origin.</param>
        /// <param name="stride">The source row stride.</param>
        /// <returns>The mean in the source sample precision.</returns>
        public static abstract int GetAverage8x8(ReadOnlySpan<TSample> source, int stride);

        /// <summary>
        /// Copies active palette-search samples into contiguous signed storage.
        /// </summary>
        /// <param name="block">The source block, from its top-left sample.</param>
        /// <param name="stride">The number of samples between rows of <paramref name="block"/>.</param>
        /// <param name="rows">The active row count.</param>
        /// <param name="columns">The active column count.</param>
        /// <param name="samples">The contiguous sample destination.</param>
        public static abstract void CopyPaletteSamples(
            ReadOnlySpan<TSample> block,
            int stride,
            int rows,
            int columns,
            Span<short> samples);

        /// <summary>
        /// Builds palette prediction and the matching source residual for transform search.
        /// </summary>
        /// <param name="source">The source block, from its top-left sample.</param>
        /// <param name="sourceStride">The number of samples between rows of <paramref name="source"/>.</param>
        /// <param name="paletteColors">The palette colors in index order.</param>
        /// <param name="colorIndexMap">The complete padded color-index map.</param>
        /// <param name="prediction">The prediction destination, from its top-left sample.</param>
        /// <param name="predictionStride">The number of samples between rows of <paramref name="prediction"/>.</param>
        /// <param name="residual">The contiguous source-minus-prediction destination.</param>
        /// <param name="transformSize">The prediction dimensions.</param>
        public static abstract void PreparePalette(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<ushort> paletteColors,
            Av1PlaneRegion<byte> colorIndexMap,
            Span<TSample> prediction,
            int predictionStride,
            Span<short> residual,
            Av1TransformSize transformSize);

        /// <summary>
        /// Builds the zero-mean Q3 luma surface shared by chroma-from-luma candidates.
        /// </summary>
        /// <param name="reconstruction">The reconstructed luma block, from its top-left sample.</param>
        /// <param name="reconstructionStride">The number of samples between rows of <paramref name="reconstruction"/>.</param>
        /// <param name="lumaQ3">The fixed-stride Q3 predictor workspace.</param>
        /// <param name="transformSize">The chroma transform dimensions.</param>
        /// <param name="lumaExtent">The luma samples the encoder coded for this block.</param>
        /// <param name="subsamplingX">Whether luma is subsampled horizontally for chroma.</param>
        /// <param name="subsamplingY">Whether luma is subsampled vertically for chroma.</param>
        public static abstract void PrepareChromaFromLuma(
            ReadOnlySpan<TSample> reconstruction,
            int reconstructionStride,
            Span<short> lumaQ3,
            Av1TransformSize transformSize,
            Size lumaExtent,
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
        /// Predicts one reference of a compound block into the unsigned compound intermediate, packed at the block width.
        /// </summary>
        /// <param name="reference">The complete bordered reference plane.</param>
        /// <param name="referenceStride">The reference row stride.</param>
        /// <param name="referenceOrigin">The integer reference origin preceding the subpixel phase.</param>
        /// <param name="intermediate">The compound intermediate destination, packed at the block width.</param>
        /// <param name="intermediateRows">The signed intermediate convolution storage.</param>
        /// <param name="width">The prediction width.</param>
        /// <param name="height">The prediction height.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="horizontalPhase">The horizontal phase in one-sixteenth-sample units.</param>
        /// <param name="verticalPhase">The vertical phase in one-sixteenth-sample units.</param>
        /// <param name="bitDepth">The coded component precision.</param>
        public static abstract void BuildCompoundIntermediate(
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int referenceOrigin,
            Span<ushort> intermediate,
            Span<short> intermediateRows,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            int bitDepth);

        /// <summary>
        /// Rounds the average of two compound intermediates to sample precision. All buffers are packed at the block width.
        /// </summary>
        /// <param name="prediction">The prediction destination.</param>
        /// <param name="first">The first compound intermediate.</param>
        /// <param name="second">The second compound intermediate.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="bitDepth">The coded component precision.</param>
        public static abstract void AverageCompoundIntermediates(
            Span<TSample> prediction,
            ReadOnlySpan<ushort> first,
            ReadOnlySpan<ushort> second,
            int width,
            int height,
            int bitDepth);

        /// <summary>
        /// Rounds the distance-weighted sum of two compound intermediates to sample precision. All buffers are packed at the block width.
        /// </summary>
        /// <param name="prediction">The prediction destination.</param>
        /// <param name="first">The first compound intermediate.</param>
        /// <param name="second">The second compound intermediate.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="firstWeight">The distance weight of the first intermediate.</param>
        /// <param name="secondWeight">The distance weight of the second intermediate.</param>
        /// <param name="bitDepth">The coded component precision.</param>
        public static abstract void WeightCompoundIntermediates(
            Span<TSample> prediction,
            ReadOnlySpan<ushort> first,
            ReadOnlySpan<ushort> second,
            int width,
            int height,
            int firstWeight,
            int secondWeight,
            int bitDepth);

        /// <summary>
        /// Blends two compound intermediates through a six-bit mask and rounds to sample precision. The prediction and both
        /// intermediates are packed at the block width.
        /// </summary>
        /// <param name="prediction">The prediction destination.</param>
        /// <param name="first">The first compound intermediate.</param>
        /// <param name="second">The second compound intermediate.</param>
        /// <param name="mask">The luma-resolution weights of the first intermediate.</param>
        /// <param name="maskStride">The mask row stride.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="subsamplingX">The horizontal subsampling shift of the plane.</param>
        /// <param name="subsamplingY">The vertical subsampling shift of the plane.</param>
        /// <param name="bitDepth">The coded component precision.</param>
        public static abstract void BlendCompoundIntermediates(
            Span<TSample> prediction,
            ReadOnlySpan<ushort> first,
            ReadOnlySpan<ushort> second,
            ReadOnlySpan<byte> mask,
            int maskStride,
            int width,
            int height,
            int subsamplingX,
            int subsamplingY,
            int bitDepth);

        /// <summary>
        /// Builds a difference-weighted mask from two rounded single-reference predictors.
        /// </summary>
        /// <param name="mask">The contiguous destination weights for the first predictor.</param>
        /// <param name="first">The first contiguous predictor.</param>
        /// <param name="second">The second contiguous predictor.</param>
        /// <param name="size">The prediction dimensions.</param>
        /// <param name="bitDepth">The sample precision.</param>
        /// <param name="maskType">The orientation of the difference weights.</param>
        public static abstract void BuildCompoundDifferenceMask(
            Span<byte> mask,
            ReadOnlySpan<TSample> first,
            ReadOnlySpan<TSample> second,
            Size size,
            Av1BitDepth bitDepth,
            Av1DifferenceWeightedMaskType maskType);

        /// <summary>
        /// Builds one filter-intra prediction for reuse across transform candidates.
        /// </summary>
        /// <param name="transformWorkspace">The transform workspace of the block, which holds the filter intra rows.</param>
        /// <param name="source">The source transform block, from its top-left sample.</param>
        /// <param name="sourceStride">The number of samples between rows of <paramref name="source"/>.</param>
        /// <param name="prediction">The prediction destination, from its top-left sample.</param>
        /// <param name="predictionStride">The number of samples between rows of <paramref name="prediction"/>.</param>
        /// <param name="above">The top reference samples, with prefix storage for the shared corner.</param>
        /// <param name="left">The left reference samples.</param>
        /// <param name="residual">The contiguous source-minus-prediction destination.</param>
        /// <param name="filterIntraMode">The selected filter-intra mode.</param>
        /// <param name="transformSize">The prediction dimensions.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        public static abstract void PrepareFilterIntra(
            Span<int> transformWorkspace,
            ReadOnlySpan<TSample> source,
            int sourceStride,
            Span<TSample> prediction,
            int predictionStride,
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
        /// <param name="sourceSamples">The samples of the complete source plane, read once by the caller.</param>
        /// <param name="blockOrigin">The destination block origin in plane samples.</param>
        /// <param name="reconstruction">The reconstructed plane containing the reference samples.</param>
        /// <param name="reconstructionSamples">The samples of the complete reconstruction plane, read once by the caller.</param>
        /// <param name="predictionOrigin">The integer reference origin preceding any half-sample phase.</param>
        /// <param name="halfX">Indicates whether the horizontal source phase is one half-sample.</param>
        /// <param name="halfY">Indicates whether the vertical source phase is one half-sample.</param>
        /// <param name="prediction">The contiguous prediction destination.</param>
        /// <param name="residual">The contiguous source-minus-prediction destination.</param>
        /// <param name="predictionSize">The prediction dimensions.</param>
        public static abstract void PrepareIntraBlockCopyPrediction(
            Av1PlaneRegion<TSample> source,
            ReadOnlySpan<TSample> sourceSamples,
            Point blockOrigin,
            Av1PlaneRegion<TSample> reconstruction,
            ReadOnlySpan<TSample> reconstructionSamples,
            Point predictionOrigin,
            bool halfX,
            bool halfY,
            Span<TSample> prediction,
            Span<short> residual,
            Av1BlockSize predictionSize);

        /// <summary>
        /// Predicts a block with an affine warped model.
        /// </summary>
        /// <param name="reference">The padded retained reference plane.</param>
        /// <param name="referenceSamples">The samples of the complete reference plane, read once by the caller.</param>
        /// <param name="referenceWidth">The visible width of the reference plane.</param>
        /// <param name="referenceHeight">The visible height of the reference plane.</param>
        /// <param name="blockOrigin">The block origin in plane samples.</param>
        /// <param name="width">The prediction width.</param>
        /// <param name="height">The prediction height.</param>
        /// <param name="subsamplingX">The horizontal subsampling of the plane.</param>
        /// <param name="subsamplingY">The vertical subsampling of the plane.</param>
        /// <param name="parameters">The warped model.</param>
        /// <param name="prediction">The contiguous prediction destination.</param>
        /// <param name="intermediateTile">The warp filter intermediate storage.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        public static abstract void PrepareWarpedInterPrediction(
            Av1PlaneRegion<TSample> reference,
            ReadOnlySpan<TSample> referenceSamples,
            int referenceWidth,
            int referenceHeight,
            Point blockOrigin,
            int width,
            int height,
            int subsamplingX,
            int subsamplingY,
            Av1GlobalMotionParameters parameters,
            Span<TSample> prediction,
            Span<short> intermediateTile,
            Av1BitDepth bitDepth);

        /// <summary>
        /// Predicts one reference of a compound block with an affine warped model into the unsigned compound intermediate. The warp uses
        /// the compound convolution rounding.
        /// </summary>
        /// <param name="reference">The padded retained reference plane.</param>
        /// <param name="referenceSamples">The samples of the complete reference plane, read once by the caller.</param>
        /// <param name="referenceWidth">The visible width of the reference plane.</param>
        /// <param name="referenceHeight">The visible height of the reference plane.</param>
        /// <param name="blockOrigin">The block origin in plane samples.</param>
        /// <param name="width">The prediction width.</param>
        /// <param name="height">The prediction height.</param>
        /// <param name="subsamplingX">The horizontal subsampling of the plane.</param>
        /// <param name="subsamplingY">The vertical subsampling of the plane.</param>
        /// <param name="parameters">The warped model.</param>
        /// <param name="intermediate">The contiguous compound intermediate destination.</param>
        /// <param name="intermediateTile">The warp filter intermediate storage.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        public static abstract void PrepareWarpedCompoundIntermediate(
            Av1PlaneRegion<TSample> reference,
            ReadOnlySpan<TSample> referenceSamples,
            int referenceWidth,
            int referenceHeight,
            Point blockOrigin,
            int width,
            int height,
            int subsamplingX,
            int subsamplingY,
            Av1GlobalMotionParameters parameters,
            Span<ushort> intermediate,
            Span<short> intermediateTile,
            Av1BitDepth bitDepth);

        /// <summary>
        /// Blends a second prediction into a destination with 6-bit weights on the destination.
        /// </summary>
        /// <param name="destination">The destination, which is also the first prediction.</param>
        /// <param name="destinationStride">The destination stride.</param>
        /// <param name="second">The second prediction.</param>
        /// <param name="secondStride">The second prediction stride.</param>
        /// <param name="mask">The weights of the destination.</param>
        /// <param name="maskStride">The mask stride, zero to repeat one row.</param>
        /// <param name="width">The blended width.</param>
        /// <param name="height">The blended height.</param>
        public static abstract void BlendMask(
            Span<TSample> destination,
            int destinationStride,
            ReadOnlySpan<TSample> second,
            int secondStride,
            ReadOnlySpan<byte> mask,
            int maskStride,
            int width,
            int height);

        /// <summary>
        /// Applies the selected luma adjustment to a DC-predicted chroma block.
        /// </summary>
        /// <param name="lumaQ3">The zero-mean Q3 luma surface.</param>
        /// <param name="prediction">
        /// The prediction receiving the adjustment, from its top-left sample. Only its first sample must hold the DC prediction:
        /// every sample is computed from that value.
        /// </param>
        /// <param name="predictionStride">The number of samples between rows of <paramref name="prediction"/>.</param>
        /// <param name="alphaQ3">The signed Q3 scale.</param>
        /// <param name="transformSize">The chroma dimensions.</param>
        /// <param name="bitDepth">The coded sample precision.</param>
        public static abstract void ApplyChromaFromLuma(
            ReadOnlySpan<short> lumaQ3,
            Span<TSample> prediction,
            int predictionStride,
            int alphaQ3,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth);

        /// <summary>
        /// Adds a selected transform's residual to the destination prediction.
        /// </summary>
        /// <param name="dequantizedCoefficients">The dequantized coefficients of the transform.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the inverse transform.</param>
        /// <param name="prediction">The destination starting at the transform origin.</param>
        /// <param name="stride">The destination row stride in samples.</param>
        /// <param name="transformSize">The transform dimensions.</param>
        /// <param name="plane">The component being reconstructed.</param>
        /// <param name="bitDepth">The coded sample precision.</param>
        /// <param name="lossless">Whether the reversible transform is required.</param>
        /// <param name="state">The nonempty transform's type and end-of-block position.</param>
        public static abstract void AddSelectedResidual(
            ReadOnlySpan<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            Span<TSample> prediction,
            int stride,
            Av1TransformSize transformSize,
            Av1Plane plane,
            Av1BitDepth bitDepth,
            bool lossless,
            Av1EncoderTransformBlockState state);

        /// <summary>
        /// Measures the rounded OBMC absolute differences of a prediction against the OBMC search target.
        /// </summary>
        /// <param name="prediction">The prediction samples at the block origin.</param>
        /// <param name="predictionStride">The prediction row stride.</param>
        /// <param name="weightedSource">The weighted source, packed at the block width.</param>
        /// <param name="mask">The prediction weights, packed at the block width.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <returns>The sum of the rounded absolute differences.</returns>
        public static abstract int SumObmcAbsoluteDifferences(
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height);

        /// <summary>
        /// Measures the signed and squared sums of the rounded OBMC differences of a prediction.
        /// </summary>
        /// <param name="prediction">The prediction samples at the block origin.</param>
        /// <param name="predictionStride">The prediction row stride.</param>
        /// <param name="weightedSource">The weighted source, packed at the block width.</param>
        /// <param name="mask">The prediction weights, packed at the block width.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="sum">The signed sum of the rounded differences.</param>
        /// <param name="squares">The sum of their squares.</param>
        public static abstract void GetObmcMoments(
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height,
            out int sum,
            out ulong squares);

        /// <summary>
        /// Writes one row of the above-neighbor term of the OBMC search target.
        /// </summary>
        /// <param name="prediction">The above-neighbor prediction row.</param>
        /// <param name="weight">The weight of the block's own prediction on this row.</param>
        /// <param name="weightedSource">The weighted source row to write.</param>
        /// <param name="mask">The prediction weight row to write.</param>
        /// <param name="width">The number of samples to write.</param>
        public static abstract void WeightObmcAbove(ReadOnlySpan<TSample> prediction, int weight, Span<int> weightedSource, Span<int> mask, int width);

        /// <summary>
        /// Blends one row of the left-neighbor term into the OBMC search target.
        /// </summary>
        /// <param name="prediction">The left-neighbor prediction row.</param>
        /// <param name="weights">The column weights of the block's own prediction, one per overlapped column.</param>
        /// <param name="weightedSource">The weighted source row to update.</param>
        /// <param name="mask">The prediction weight row to update.</param>
        public static abstract void WeightObmcLeft(ReadOnlySpan<TSample> prediction, ReadOnlySpan<byte> weights, Span<int> weightedSource, Span<int> mask);

        /// <summary>
        /// Replaces one row of OBMC neighbor terms with the scaled source minus the term.
        /// </summary>
        /// <param name="source">The source row.</param>
        /// <param name="weightedSource">The weighted source row to update.</param>
        public static abstract void SubtractObmcSource(ReadOnlySpan<TSample> source, Span<int> weightedSource);
    }

    /// <summary>
    /// Predicts one translational rectangle from a retained reference frame into a strided destination, without a residual.
    /// </summary>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample-storage operations.</typeparam>
    /// <param name="reference">The padded retained reference plane.</param>
    /// <param name="referenceSamples">The samples of the complete reference plane, read once by the caller.</param>
    /// <param name="predictionOrigin">The integer reference origin preceding the subpixel phase.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The horizontal phase in one-sixteenth-sample units.</param>
    /// <param name="verticalPhase">The vertical phase in one-sixteenth-sample units.</param>
    /// <param name="prediction">The prediction destination, starting at the rectangle.</param>
    /// <param name="predictionStride">The destination stride.</param>
    /// <param name="width">The rectangle width.</param>
    /// <param name="height">The rectangle height.</param>
    /// <param name="filterRows">The intermediate storage used by two-dimensional filtering.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    private static void PredictTranslationalInter<TSample, TOperator>(
        Av1PlaneRegion<TSample> reference,
        ReadOnlySpan<TSample> referenceSamples,
        Point predictionOrigin,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int verticalPhase,
        Span<TSample> prediction,
        int predictionStride,
        int width,
        int height,
        Span<short> filterRows,
        Av1BitDepth bitDepth)
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        // The origin indexes the complete padded plane, so the plane bounds move the prediction origin into it.
        Rectangle referenceBounds = reference.Bounds;
        int referenceOrigin =
            ((referenceBounds.Y + predictionOrigin.Y) * reference.Stride) +
            referenceBounds.X +
            predictionOrigin.X;

        TOperator.BuildPrediction(
            referenceSamples,
            reference.Stride,
            referenceOrigin,
            prediction,
            predictionStride,
            filterRows,
            width,
            height,
            horizontalFilter,
            verticalFilter,
            horizontalPhase,
            verticalPhase,
            bitDepth.GetBitCount());
    }

    /// <summary>
    /// Builds a translational prediction from a retained reference frame and the matching source residual.
    /// </summary>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample-storage operations.</typeparam>
    /// <param name="source">The coded source plane.</param>
    /// <param name="sourceSamples">The samples of the complete source plane, read once by the caller.</param>
    /// <param name="blockOrigin">The destination block origin in plane samples.</param>
    /// <param name="reference">The padded retained reference plane.</param>
    /// <param name="referenceSamples">The samples of the complete reference plane, read once by the caller.</param>
    /// <param name="predictionOrigin">The integer reference origin preceding the subpixel phase.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
    /// <param name="verticalFilter">The vertical interpolation filter.</param>
    /// <param name="horizontalPhase">The horizontal phase in one-sixteenth-sample units.</param>
    /// <param name="verticalPhase">The vertical phase in one-sixteenth-sample units.</param>
    /// <param name="prediction">The contiguous prediction destination.</param>
    /// <param name="residual">The contiguous source-minus-prediction destination.</param>
    /// <param name="filterRows">The intermediate storage used by two-dimensional filtering.</param>
    /// <param name="predictionSize">The prediction dimensions.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    private static void PrepareTranslationalInterPrediction<TSample, TOperator>(
        Av1PlaneRegion<TSample> source,
        ReadOnlySpan<TSample> sourceSamples,
        Point blockOrigin,
        Av1PlaneRegion<TSample> reference,
        ReadOnlySpan<TSample> referenceSamples,
        Point predictionOrigin,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        int horizontalPhase,
        int verticalPhase,
        Span<TSample> prediction,
        Span<short> residual,
        Span<short> filterRows,
        Av1BlockSize predictionSize,
        Av1BitDepth bitDepth)
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        int width = predictionSize.GetWidth();
        int height = predictionSize.GetHeight();

        // The prediction is packed, so the block width is the stride of both the prediction and the residual.
        PredictTranslationalInter<TSample, TOperator>(
            reference,
            referenceSamples,
            predictionOrigin,
            horizontalFilter,
            verticalFilter,
            horizontalPhase,
            verticalPhase,
            prediction,
            width,
            width,
            height,
            filterRows,
            bitDepth);

        TOperator.SubtractPrediction(
            Av1TransformBlockEncoder.GetPlaneSpan(sourceSamples, source, blockOrigin), source.Stride, prediction, width, residual, width, height);
    }

    /// <summary>
    /// Builds a compound prediction from two retained reference frames using the selected blend, and the matching source residual.
    /// </summary>
    /// <typeparam name="TSample">The native unsigned sample storage type.</typeparam>
    /// <typeparam name="TOperator">The sample-storage operations.</typeparam>
    /// <param name="source">The coded source plane.</param>
    /// <param name="sourceSamples">The samples of the complete source plane, read once by the caller.</param>
    /// <param name="blockOrigin">The destination block origin in plane samples.</param>
    /// <param name="primaryReference">The padded primary retained reference plane.</param>
    /// <param name="primaryReferenceSamples">The samples of the complete primary reference plane, read once by the caller.</param>
    /// <param name="primaryPredictionOrigin">The integer primary-reference origin preceding the subpixel phase.</param>
    /// <param name="primaryHorizontalPhase">The primary horizontal phase in one-sixteenth-sample units.</param>
    /// <param name="primaryVerticalPhase">The primary vertical phase in one-sixteenth-sample units.</param>
    /// <param name="secondaryReference">The padded secondary retained reference plane.</param>
    /// <param name="secondaryReferenceSamples">The samples of the complete secondary reference plane, read once by the caller.</param>
    /// <param name="secondaryPredictionOrigin">The integer secondary-reference origin preceding the subpixel phase.</param>
    /// <param name="secondaryHorizontalPhase">The secondary horizontal phase in one-sixteenth-sample units.</param>
    /// <param name="secondaryVerticalPhase">The secondary vertical phase in one-sixteenth-sample units.</param>
    /// <param name="horizontalFilter">The horizontal interpolation filter shared by both references.</param>
    /// <param name="verticalFilter">The vertical interpolation filter shared by both references.</param>
    /// <param name="prediction">The contiguous blended prediction destination.</param>
    /// <param name="residual">The contiguous source-minus-prediction destination.</param>
    /// <param name="firstIntermediate">The reusable primary unsigned compound intermediate.</param>
    /// <param name="secondIntermediate">The reusable secondary unsigned compound intermediate.</param>
    /// <param name="compoundMask">The reusable luma-resolution blend mask.</param>
    /// <param name="filterRows">The intermediate storage used by two-dimensional filtering.</param>
    /// <param name="predictionSize">The prediction dimensions.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="plane">The component plane. Only luma constructs the difference-weighted mask.</param>
    /// <param name="lumaBlockSize">The luma block dimensions used to construct the blend mask.</param>
    /// <param name="compoundType">The blend applied to the two predictors.</param>
    /// <param name="firstWeight">The primary predictor's distance weight.</param>
    /// <param name="secondWeight">The secondary predictor's distance weight.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <param name="subsamplingY">The vertical chroma subsampling shift.</param>
    /// <param name="wedgeIndex">The selected wedge shape.</param>
    /// <param name="wedgeSign">Whether to reverse the wedge's predictor weights.</param>
    /// <param name="differenceWeightedMaskType">The polarity of the difference-weighted mask.</param>
    /// <param name="primaryPrepared">Whether <paramref name="firstIntermediate"/> already holds the primary predictor.</param>
    /// <param name="secondaryPrepared">Whether <paramref name="secondIntermediate"/> already holds the secondary predictor.</param>
    private static void PrepareCompoundInterPrediction<TSample, TOperator>(
        Av1PlaneRegion<TSample> source,
        ReadOnlySpan<TSample> sourceSamples,
        Point blockOrigin,
        Av1PlaneRegion<TSample> primaryReference,
        ReadOnlySpan<TSample> primaryReferenceSamples,
        Point primaryPredictionOrigin,
        int primaryHorizontalPhase,
        int primaryVerticalPhase,
        Av1PlaneRegion<TSample> secondaryReference,
        ReadOnlySpan<TSample> secondaryReferenceSamples,
        Point secondaryPredictionOrigin,
        int secondaryHorizontalPhase,
        int secondaryVerticalPhase,
        Av1InterpolationFilter horizontalFilter,
        Av1InterpolationFilter verticalFilter,
        Span<TSample> prediction,
        Span<short> residual,
        Span<ushort> firstIntermediate,
        Span<ushort> secondIntermediate,
        Span<byte> compoundMask,
        Span<short> filterRows,
        Av1BlockSize predictionSize,
        Av1BitDepth bitDepth,
        Av1Plane plane,
        Av1BlockSize lumaBlockSize,
        Av1CompoundType compoundType,
        int firstWeight,
        int secondWeight,
        int subsamplingX,
        int subsamplingY,
        int wedgeIndex,
        bool wedgeSign,
        Av1DifferenceWeightedMaskType differenceWeightedMaskType,
        bool primaryPrepared,
        bool secondaryPrepared)
        where TSample : unmanaged
        where TOperator : struct, IBlockEncodingOperator<TSample>
    {
        // The prediction, the residual and both intermediates are packed at the block width.
        int width = predictionSize.GetWidth();
        int height = predictionSize.GetHeight();
        int bitCount = bitDepth.GetBitCount();

        // Each origin indexes the complete padded plane, so the plane bounds move the prediction origin into it.
        Rectangle primaryBounds = primaryReference.Bounds;
        int primaryOrigin = ((primaryBounds.Y + primaryPredictionOrigin.Y) * primaryReference.Stride) +
            primaryBounds.X + primaryPredictionOrigin.X;

        Rectangle secondaryBounds = secondaryReference.Bounds;
        int secondaryOrigin = ((secondaryBounds.Y + secondaryPredictionOrigin.Y) * secondaryReference.Stride) +
            secondaryBounds.X + secondaryPredictionOrigin.X;

        if (!primaryPrepared)
        {
            TOperator.BuildCompoundIntermediate(
                primaryReferenceSamples,
                primaryReference.Stride,
                primaryOrigin,
                firstIntermediate,
                filterRows,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                primaryHorizontalPhase,
                primaryVerticalPhase,
                bitCount);
        }

        if (!secondaryPrepared)
        {
            TOperator.BuildCompoundIntermediate(
                secondaryReferenceSamples,
                secondaryReference.Stride,
                secondaryOrigin,
                secondIntermediate,
                filterRows,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                secondaryHorizontalPhase,
                secondaryVerticalPhase,
                bitCount);
        }

        switch (compoundType)
        {
            case Av1CompoundType.DistanceWeighted:
                TOperator.WeightCompoundIntermediates(prediction, firstIntermediate, secondIntermediate, width, height, firstWeight, secondWeight, bitCount);
                break;
            case Av1CompoundType.Wedge:
                Av1WedgeMask.Fill(compoundMask, lumaBlockSize.GetWidth(), lumaBlockSize, wedgeIndex, wedgeSign, 0, 0, invert: false);
                TOperator.BlendCompoundIntermediates(
                    prediction,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    lumaBlockSize.GetWidth(),
                    width,
                    height,
                    subsamplingX,
                    subsamplingY,
                    bitCount);

                break;
            case Av1CompoundType.DifferenceWeighted:
                // Chroma reuses the luma mask even when its sample dimensions are identical.
                if (plane == Av1Plane.Y)
                {
                    Av1CompoundIntermediateDifferenceWeightedMaskBuilder.FillDifferenceWeightedIntermediateMask(
                        compoundMask,
                        width,
                        firstIntermediate,
                        width,
                        secondIntermediate,
                        width,
                        width,
                        height,
                        bitCount,
                        differenceWeightedMaskType);
                }

                TOperator.BlendCompoundIntermediates(
                    prediction,
                    firstIntermediate,
                    secondIntermediate,
                    compoundMask,
                    lumaBlockSize.GetWidth(),
                    width,
                    height,
                    subsamplingX,
                    subsamplingY,
                    bitCount);

                break;
            default:
                TOperator.AverageCompoundIntermediates(prediction, firstIntermediate, secondIntermediate, width, height, bitCount);
                break;
        }

        TOperator.SubtractPrediction(
            Av1TransformBlockEncoder.GetPlaneSpan(sourceSamples, source, blockOrigin), source.Stride, prediction, width, residual, width, height);
    }

    /// <summary>
    /// Encodes blocks stored as eight-bit samples.
    /// </summary>
    internal readonly struct ByteOperator : IBlockEncodingOperator<byte>
    {
        /// <inheritdoc/>
        public static void BuildPrediction(
            ReadOnlySpan<byte> reference,
            int referenceStride,
            int referenceOrigin,
            Span<byte> prediction,
            int predictionStride,
            Span<short> intermediateRows,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            int bitDepth)
            => Av1TranslationalInterPredictor.Predict(
                reference,
                referenceStride,
                referenceOrigin,
                prediction,
                predictionStride,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                intermediateRows);

        /// <inheritdoc/>
        public static void SubtractPrediction(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            Span<short> residual,
            int width,
            int height)
            => Av1ResidualBuilder.Subtract<byte, Av1ResidualBuilder.ByteOperator>(
                source, sourceStride, prediction, predictionStride, residual, width, width, height);

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
            => Av1TranslationalInterPredictor.PredictForSearch(
                reference, referenceStride, referenceOrigin, buffer, width, height, horizontalPhase, verticalPhase, taps);

        /// <inheritdoc/>
        public static void PredictScaled(
            ReadOnlySpan<byte> reference,
            int referenceStride,
            int referenceOrigin,
            Span<byte> buffer,
            int bufferStride,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int horizontalStep,
            int verticalPhase,
            int verticalStep,
            Span<short> intermediateRows,
            int bitDepth)
            => Av1ScaledInterPredictor.PredictScaled(
                reference,
                referenceStride,
                referenceOrigin,
                buffer,
                bufferStride,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                horizontalStep,
                verticalPhase,
                verticalStep,
                intermediateRows);

        /// <inheritdoc/>
        public static int SumAbsoluteDifferences(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            int width,
            int height,
            int rowStep)
            => Av1ResidualBuilder.SumAbsoluteDifferences<byte, Av1ResidualBuilder.ByteOperator>(
                source, sourceStride, prediction, predictionStride, width, height, rowStep);

        /// <inheritdoc/>
        public static int SumCompoundAbsoluteDifferences(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            ReadOnlySpan<byte> secondPrediction,
            ReadOnlySpan<byte> mask,
            int width,
            int height,
            int rowStep)
            => Av1ResidualBuilder.SumCompoundAbsoluteDifferences<byte, Av1ResidualBuilder.ByteOperator>(
                source, sourceStride, prediction, predictionStride, secondPrediction, mask, width, height, rowStep);

        /// <inheritdoc/>
        public static void GetCompoundMoments(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            ReadOnlySpan<byte> secondPrediction,
            ReadOnlySpan<byte> mask,
            int width,
            int height,
            out int sum,
            out long squares)
            => Av1ResidualBuilder.GetCompoundMoments<byte, Av1ResidualBuilder.ByteOperator>(
                source, sourceStride, prediction, predictionStride, secondPrediction, mask, width, height, out sum, out squares);

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
            => Av1ResidualBuilder.GetMoments<byte, Av1ResidualBuilder.ByteOperator>(
                source, sourceStride, prediction, predictionStride, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static int SumObmcAbsoluteDifferences(
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height)
            => Av1ObmcSearch.SumAbsoluteDifferences<byte, Av1ObmcSearch.ByteOperator>(prediction, predictionStride, weightedSource, mask, width, height);

        /// <inheritdoc/>
        public static void GetObmcMoments(
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height,
            out int sum,
            out ulong squares)
            => Av1ObmcSearch.GetMoments<byte, Av1ObmcSearch.ByteOperator>(
                prediction, predictionStride, weightedSource, mask, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static void WeightObmcAbove(ReadOnlySpan<byte> prediction, int weight, Span<int> weightedSource, Span<int> mask, int width)
            => Av1ObmcSearch.WeightAbove<byte, Av1ObmcSearch.ByteOperator>(prediction, weight, weightedSource, mask, width);

        /// <inheritdoc/>
        public static void WeightObmcLeft(ReadOnlySpan<byte> prediction, ReadOnlySpan<byte> weights, Span<int> weightedSource, Span<int> mask)
            => Av1ObmcSearch.WeightLeft<byte, Av1ObmcSearch.ByteOperator>(prediction, weights, weightedSource, mask);

        /// <inheritdoc/>
        public static void SubtractObmcSource(ReadOnlySpan<byte> source, Span<int> weightedSource)
            => Av1ObmcSearch.SubtractFromSource<byte, Av1ObmcSearch.ByteOperator>(source, weightedSource);

        /// <inheritdoc/>
        public static byte CreateSample(int value) => (byte)value;

        /// <inheritdoc/>
        public static int GetSampleValue(byte sample) => sample;

        /// <inheritdoc/>
        public static long GetVarianceStatistic(
            ReadOnlySpan<byte> source,
            int stride,
            int width,
            int height,
            int visibleWidth,
            int visibleHeight)
            => Av1VarianceStatistic.CalculateWithBorder<byte, Av1MotionVectorStatistics.ByteTextureOperator>(
                source, stride, width, height, visibleWidth, visibleHeight);

        /// <inheritdoc/>
        public static int GetAverage4x4(ReadOnlySpan<byte> source, int stride)
        {
            // Four packed rows occupy sixteen byte lanes. Widen before summing to retain all eight sample bits.
            Vector128<byte> samples = Vector128.Create(
                MemoryMarshal.Read<uint>(source[..4]),
                MemoryMarshal.Read<uint>(source.Slice(stride, 4)),
                MemoryMarshal.Read<uint>(source.Slice(2 * stride, 4)),
                MemoryMarshal.Read<uint>(source.Slice(3 * stride, 4))).AsByte();

            int sum = Vector128.Sum(Vector128.WidenLower(samples)) + Vector128.Sum(Vector128.WidenUpper(samples));
            return (sum + 8) >> 4;
        }

        /// <inheritdoc/>
        public static int GetAverage8x8(ReadOnlySpan<byte> source, int stride)
        {
            // Each widened lane accumulates one column from eight rows. The column sums fit in eleven bits, and their total fits in a
            // ushort. The average rounds once, after the complete sum.
            Vector128<ushort> columns = Vector128<ushort>.Zero;
            for (int row = 0; row < 8; row++)
            {
                Vector64<byte> samples = Vector64.LoadUnsafe(ref MemoryMarshal.GetReference(source), (nuint)(row * stride));
                columns += Vector128.WidenLower(Vector128.Create(samples, Vector64<byte>.Zero));
            }

            return (Vector128.Sum(columns) + 32) >> 6;
        }

        /// <inheritdoc/>
        public static uint GetHashSample(byte sample) => sample;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> LoadHashBytes(ref byte source, nuint offset, Vector128<uint> lanes)
        {
            Vector128<byte> samples = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, offset))).AsByte();
            return Vector128.WidenLower(Vector128.WidenLower(samples));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> LoadHashBytes(ref byte source, nuint offset, Vector256<uint> lanes)
            => Vector256.WidenLower(Vector128.WidenLower(Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, offset))).AsByte()).ToVector256Unsafe());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> LoadHashBytes(ref byte source, nuint offset, Vector512<uint> lanes)
            => Vector512.WidenLower(Vector256.WidenLower(Vector128.LoadUnsafe(ref source, offset).ToVector256Unsafe()).ToVector512Unsafe());

        /// <inheritdoc/>
        public static bool BlocksEqual(ReadOnlySpan<byte> first, int firstStride, ReadOnlySpan<byte> second, int secondStride)
        {
            for (int row = 0; row < 8; row++)
            {
                // One 64-bit comparison covers the complete row. Equality of the packed bytes does not depend on
                // native endianness.
                if (MemoryMarshal.Read<ulong>(first[(row * firstStride)..]) != MemoryMarshal.Read<ulong>(second[(row * secondStride)..]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public static bool IsHorizontalPerfect(ReadOnlySpan<byte> block, int stride)
        {
            for (int row = 0; row < 8; row++)
            {
                // A row is flat when its packed bytes equal one of its bytes broadcast to all eight, which holds for
                // either byte order.
                ulong samples = MemoryMarshal.Read<ulong>(block[(row * stride)..]);
                if (samples != (samples & 0xFF) * 0x0101010101010101UL)
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public static bool IsVerticalPerfect(ReadOnlySpan<byte> block, int stride)
        {
            // Every column repeats its first sample when every row equals the first row.
            ulong first = MemoryMarshal.Read<ulong>(block);
            for (int row = 1; row < 8; row++)
            {
                if (MemoryMarshal.Read<ulong>(block[(row * stride)..]) != first)
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public static void CopyPaletteSamples(
            ReadOnlySpan<byte> block,
            int stride,
            int rows,
            int columns,
            Span<short> samples)
        {
            for (int row = 0; row < rows; row++)
            {
                WidenSamples(block.Slice(row * stride, columns), samples.Slice(row * columns, columns));
            }
        }

        /// <summary>
        /// Widens eight-bit samples to sixteen bits, widest vectors first.
        /// </summary>
        /// <param name="source">The eight-bit samples.</param>
        /// <param name="destination">Receives the widened samples.</param>
        private static void WidenSamples(ReadOnlySpan<byte> source, Span<short> destination)
        {
            ref byte sourceBase = ref MemoryMarshal.GetReference(source);
            ref short destinationBase = ref MemoryMarshal.GetReference(destination);
            int length = source.Length;
            int column = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; column <= length - Vector256<byte>.Count; column += Vector256<byte>.Count)
                {
                    Vector512_.Widen(Vector256.LoadUnsafe(ref sourceBase, (nuint)column)).StoreUnsafe(ref destinationBase, (nuint)column);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; column <= length - Vector128<byte>.Count; column += Vector128<byte>.Count)
                {
                    Vector256_.Widen(Vector128.LoadUnsafe(ref sourceBase, (nuint)column)).StoreUnsafe(ref destinationBase, (nuint)column);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; column <= length - 8; column += 8)
                {
                    Vector128<byte> samples = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref sourceBase, column))).AsByte();
                    Vector128.WidenLower(samples).AsInt16().StoreUnsafe(ref destinationBase, (nuint)column);
                }
            }

            for (; column < length; column++)
            {
                Unsafe.Add(ref destinationBase, column) = Unsafe.Add(ref sourceBase, column);
            }
        }

        /// <inheritdoc/>
        public static void PreparePalette(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<ushort> paletteColors,
            Av1PlaneRegion<byte> colorIndexMap,
            Span<byte> prediction,
            int predictionStride,
            Span<short> residual,
            Av1TransformSize transformSize)
        {
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            Av1PalettePredictor.Predict(
                paletteColors,
                colorIndexMap,
                prediction,
                predictionStride,
                width,
                height);

            Av1ResidualBuilder.Subtract(
                source,
                sourceStride,
                prediction,
                predictionStride,
                residual,
                width,
                width,
                height);
        }

        /// <inheritdoc/>
        public static void PrepareChromaFromLuma(
            ReadOnlySpan<byte> reconstruction,
            int reconstructionStride,
            Span<short> lumaQ3,
            Av1TransformSize transformSize,
            Size lumaExtent,
            bool subsamplingX,
            bool subsamplingY)
            => Av1ChromaFromLumaContext.PrepareBlock(
                reconstruction,
                reconstructionStride,
                lumaQ3,
                transformSize,
                lumaExtent,
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
        public static void PrepareFilterIntra(
            Span<int> transformWorkspace,
            ReadOnlySpan<byte> source,
            int sourceStride,
            Span<byte> prediction,
            int predictionStride,
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
            Span<byte> filterRows = MemoryMarshal.AsBytes(transformWorkspace).Slice(
                0,
                Av1FilterIntraPredictorBase.BufferLength);

            Av1FilterIntraPredictorBase.GetPredictor(filterIntraMode)
                .Predict(prediction, predictionStride, above, left, width, height, filterRows);

            Av1ResidualBuilder.Subtract(
                source,
                sourceStride,
                prediction,
                predictionStride,
                residual,
                width,
                width,
                height);
        }

        /// <inheritdoc/>
        public static void PrepareIntraBlockCopyPrediction(
            Av1PlaneRegion<byte> source,
            ReadOnlySpan<byte> sourceSamples,
            Point blockOrigin,
            Av1PlaneRegion<byte> reconstruction,
            ReadOnlySpan<byte> reconstructionSamples,
            Point predictionOrigin,
            bool halfX,
            bool halfY,
            Span<byte> prediction,
            Span<short> residual,
            Av1BlockSize predictionSize)
        {
            int width = predictionSize.GetWidth();
            int height = predictionSize.GetHeight();
            Av1IntraBlockCopyPredictor.Predict(
                Av1TransformBlockEncoder.GetPlaneSpan(reconstructionSamples, reconstruction, predictionOrigin),
                reconstruction.Stride,
                prediction,
                width,
                width,
                height,
                halfX,
                halfY);

            Av1ResidualBuilder.Subtract(
                Av1TransformBlockEncoder.GetPlaneSpan(sourceSamples, source, blockOrigin),
                source.Stride,
                prediction,
                width,
                residual,
                width,
                width,
                height);
        }

        /// <inheritdoc/>
        public static void PrepareWarpedInterPrediction(
            Av1PlaneRegion<byte> reference,
            ReadOnlySpan<byte> referenceSamples,
            int referenceWidth,
            int referenceHeight,
            Point blockOrigin,
            int width,
            int height,
            int subsamplingX,
            int subsamplingY,
            Av1GlobalMotionParameters parameters,
            Span<byte> prediction,
            Span<short> intermediateTile,
            Av1BitDepth bitDepth)
            => Av1WarpedInterPredictor.PredictWarped(
                referenceSamples,
                reference.Stride,
                reference.Bounds.Location,
                referenceWidth,
                referenceHeight,
                prediction,
                width,
                blockOrigin,
                width,
                height,
                subsamplingX,
                subsamplingY,
                parameters,
                intermediateTile);

        /// <inheritdoc/>
        public static void PrepareWarpedCompoundIntermediate(
            Av1PlaneRegion<byte> reference,
            ReadOnlySpan<byte> referenceSamples,
            int referenceWidth,
            int referenceHeight,
            Point blockOrigin,
            int width,
            int height,
            int subsamplingX,
            int subsamplingY,
            Av1GlobalMotionParameters parameters,
            Span<ushort> intermediate,
            Span<short> intermediateTile,
            Av1BitDepth bitDepth)
            => Av1WarpedInterPredictor.PredictWarpedCompound(
                referenceSamples,
                reference.Stride,
                reference.Bounds.Location,
                referenceWidth,
                referenceHeight,
                intermediate,
                width,
                blockOrigin,
                width,
                height,
                subsamplingX,
                subsamplingY,
                parameters,
                intermediateTile);

        /// <inheritdoc/>
        public static void BlendMask(
            Span<byte> destination,
            int destinationStride,
            ReadOnlySpan<byte> second,
            int secondStride,
            ReadOnlySpan<byte> mask,
            int maskStride,
            int width,
            int height)
            => Av1CompoundMaskBlendPredictor.Blend(destination, destinationStride, second, secondStride, mask, maskStride, width, height);

        /// <inheritdoc/>
        public static void BuildCompoundIntermediate(
            ReadOnlySpan<byte> reference,
            int referenceStride,
            int referenceOrigin,
            Span<ushort> intermediate,
            Span<short> intermediateRows,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            int bitDepth)
            => Av1CompoundInterPredictor.PredictCompound(
                reference,
                referenceStride,
                referenceOrigin,
                intermediate,
                width,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                intermediateRows);

        /// <inheritdoc/>
        public static void AverageCompoundIntermediates(
            Span<byte> prediction,
            ReadOnlySpan<ushort> first,
            ReadOnlySpan<ushort> second,
            int width,
            int height,
            int bitDepth)
            => Av1CompoundIntermediateAveragePredictor.AverageIntermediate(prediction, width, first, width, second, width, width, height, bitDepth);

        /// <inheritdoc/>
        public static void WeightCompoundIntermediates(
            Span<byte> prediction,
            ReadOnlySpan<ushort> first,
            ReadOnlySpan<ushort> second,
            int width,
            int height,
            int firstWeight,
            int secondWeight,
            int bitDepth)
            => Av1CompoundIntermediateDistanceWeightedPredictor.DistanceWeightedIntermediate(
                prediction, width, first, width, second, width, width, height, firstWeight, secondWeight, bitDepth);

        /// <inheritdoc/>
        public static void BlendCompoundIntermediates(
            Span<byte> prediction,
            ReadOnlySpan<ushort> first,
            ReadOnlySpan<ushort> second,
            ReadOnlySpan<byte> mask,
            int maskStride,
            int width,
            int height,
            int subsamplingX,
            int subsamplingY,
            int bitDepth)
            => Av1CompoundIntermediateMaskBlendPredictor.BlendIntermediate(
                prediction, width, first, width, second, width, mask, maskStride, width, height, subsamplingX, subsamplingY, bitDepth);

        /// <inheritdoc/>
        public static void BuildCompoundDifferenceMask(
            Span<byte> mask,
            ReadOnlySpan<byte> first,
            ReadOnlySpan<byte> second,
            Size size,
            Av1BitDepth bitDepth,
            Av1DifferenceWeightedMaskType maskType)
            => Av1DifferenceWeightedMaskBuilder.FillDifferenceWeightedMask(
                mask,
                size.Width,
                first,
                size.Width,
                second,
                size.Width,
                size.Width,
                size.Height,
                maskType);

        /// <inheritdoc/>
        public static void ApplyChromaFromLuma(
            ReadOnlySpan<short> lumaQ3,
            Span<byte> prediction,
            int predictionStride,
            int alphaQ3,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth)
            => Av1ChromaFromLumaPredictor.Predict(
                lumaQ3, prediction, predictionStride, alphaQ3, transformSize.GetWidth(), transformSize.GetHeight());

        /// <inheritdoc/>
        public static void AddSelectedResidual(
            ReadOnlySpan<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            Span<byte> prediction,
            int stride,
            Av1TransformSize transformSize,
            Av1Plane plane,
            Av1BitDepth bitDepth,
            bool lossless,
            Av1EncoderTransformBlockState state)
            => Av1InverseTransformer.Reconstruct8Bit(
                dequantizedCoefficients,
                prediction,
                stride,
                transformSize,
                state.TransformType,
                (int)plane,
                state.EndOfBlock,
                lossless,
                transformWorkspace);
    }

    /// <summary>
    /// Encodes blocks stored as high-bit-depth samples.
    /// </summary>
    internal readonly struct UInt16Operator : IBlockEncodingOperator<ushort>
    {
        /// <inheritdoc/>
        public static void BuildPrediction(
            ReadOnlySpan<ushort> reference,
            int referenceStride,
            int referenceOrigin,
            Span<ushort> prediction,
            int predictionStride,
            Span<short> intermediateRows,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            int bitDepth)
            => Av1TranslationalInterPredictor.Predict(
                reference,
                referenceStride,
                referenceOrigin,
                prediction,
                predictionStride,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                bitDepth,
                intermediateRows);

        /// <inheritdoc/>
        public static void SubtractPrediction(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            Span<short> residual,
            int width,
            int height)
            => Av1ResidualBuilder.Subtract<ushort, Av1ResidualBuilder.UInt16Operator>(
                source, sourceStride, prediction, predictionStride, residual, width, width, height);

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
            => Av1TranslationalInterPredictor.PredictForSearch(
                reference, referenceStride, referenceOrigin, buffer, width, height, horizontalPhase, verticalPhase, taps, bitDepth);

        /// <inheritdoc/>
        public static void PredictScaled(
            ReadOnlySpan<ushort> reference,
            int referenceStride,
            int referenceOrigin,
            Span<ushort> buffer,
            int bufferStride,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int horizontalStep,
            int verticalPhase,
            int verticalStep,
            Span<short> intermediateRows,
            int bitDepth)
            => Av1ScaledInterPredictor.PredictScaled(
                reference,
                referenceStride,
                referenceOrigin,
                buffer,
                bufferStride,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                horizontalStep,
                verticalPhase,
                verticalStep,
                bitDepth,
                intermediateRows);

        /// <inheritdoc/>
        public static int SumAbsoluteDifferences(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            int width,
            int height,
            int rowStep)
            => Av1ResidualBuilder.SumAbsoluteDifferences<ushort, Av1ResidualBuilder.UInt16Operator>(
                source, sourceStride, prediction, predictionStride, width, height, rowStep);

        /// <inheritdoc/>
        public static int SumCompoundAbsoluteDifferences(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            ReadOnlySpan<ushort> secondPrediction,
            ReadOnlySpan<byte> mask,
            int width,
            int height,
            int rowStep)
            => Av1ResidualBuilder.SumCompoundAbsoluteDifferences<ushort, Av1ResidualBuilder.UInt16Operator>(
                source, sourceStride, prediction, predictionStride, secondPrediction, mask, width, height, rowStep);

        /// <inheritdoc/>
        public static void GetCompoundMoments(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            ReadOnlySpan<ushort> secondPrediction,
            ReadOnlySpan<byte> mask,
            int width,
            int height,
            out int sum,
            out long squares)
            => Av1ResidualBuilder.GetCompoundMoments<ushort, Av1ResidualBuilder.UInt16Operator>(
                source, sourceStride, prediction, predictionStride, secondPrediction, mask, width, height, out sum, out squares);

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
            => Av1ResidualBuilder.GetMoments<ushort, Av1ResidualBuilder.UInt16Operator>(
                source, sourceStride, prediction, predictionStride, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static int SumObmcAbsoluteDifferences(
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height)
            => Av1ObmcSearch.SumAbsoluteDifferences<ushort, Av1ObmcSearch.UInt16Operator>(prediction, predictionStride, weightedSource, mask, width, height);

        /// <inheritdoc/>
        public static void GetObmcMoments(
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height,
            out int sum,
            out ulong squares)
            => Av1ObmcSearch.GetMoments<ushort, Av1ObmcSearch.UInt16Operator>(
                prediction, predictionStride, weightedSource, mask, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static void WeightObmcAbove(ReadOnlySpan<ushort> prediction, int weight, Span<int> weightedSource, Span<int> mask, int width)
            => Av1ObmcSearch.WeightAbove<ushort, Av1ObmcSearch.UInt16Operator>(prediction, weight, weightedSource, mask, width);

        /// <inheritdoc/>
        public static void WeightObmcLeft(ReadOnlySpan<ushort> prediction, ReadOnlySpan<byte> weights, Span<int> weightedSource, Span<int> mask)
            => Av1ObmcSearch.WeightLeft<ushort, Av1ObmcSearch.UInt16Operator>(prediction, weights, weightedSource, mask);

        /// <inheritdoc/>
        public static void SubtractObmcSource(ReadOnlySpan<ushort> source, Span<int> weightedSource)
            => Av1ObmcSearch.SubtractFromSource<ushort, Av1ObmcSearch.UInt16Operator>(source, weightedSource);

        /// <inheritdoc/>
        public static ushort CreateSample(int value) => (ushort)value;

        /// <inheritdoc/>
        public static int GetSampleValue(ushort sample) => sample;

        /// <inheritdoc/>
        public static long GetVarianceStatistic(
            ReadOnlySpan<ushort> source,
            int stride,
            int width,
            int height,
            int visibleWidth,
            int visibleHeight)
            => Av1VarianceStatistic.CalculateWithBorder<ushort, Av1MotionVectorStatistics.UInt16TextureOperator>(
                source, stride, width, height, visibleWidth, visibleHeight);

        /// <inheritdoc/>
        public static int GetAverage4x4(ReadOnlySpan<ushort> source, int stride)
        {
            // Four ushort lanes accumulate matching columns. Twelve-bit samples keep both column and final sums within ushort.
            // Each row loads as one 64-bit value into the low half of a 128-bit vector, so the upper lanes stay zero.
            Vector128<ushort> columns = Vector128.CreateScalar(MemoryMarshal.Read<ulong>(MemoryMarshal.AsBytes(source[..4]))).AsUInt16() +
                Vector128.CreateScalar(MemoryMarshal.Read<ulong>(MemoryMarshal.AsBytes(source.Slice(stride, 4)))).AsUInt16() +
                Vector128.CreateScalar(MemoryMarshal.Read<ulong>(MemoryMarshal.AsBytes(source.Slice(2 * stride, 4)))).AsUInt16() +
                Vector128.CreateScalar(MemoryMarshal.Read<ulong>(MemoryMarshal.AsBytes(source.Slice(3 * stride, 4)))).AsUInt16();

            return (Vector128.Sum(columns) + 8) >> 4;
        }

        /// <inheritdoc/>
        public static int GetAverage8x8(ReadOnlySpan<ushort> source, int stride)
        {
            // Eight twelve-bit samples fit in each ushort column lane. Widen before the
            // horizontal reduction because all sixty-four samples require eighteen bits.
            Vector128<ushort> columns = Vector128<ushort>.Zero;
            for (int row = 0; row < 8; row++)
            {
                columns += Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(source), (nuint)(row * stride));
            }

            uint sum = Vector128.Sum(Vector128.WidenLower(columns)) + Vector128.Sum(Vector128.WidenUpper(columns));
            return (int)((sum + 32) >> 6);
        }

        /// <inheritdoc/>
        public static uint GetHashSample(ushort sample) => sample;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<uint> LoadHashBytes(ref ushort source, nuint offset, Vector128<uint> lanes)
        {
            Vector128<ushort> samples = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref Unsafe.Add(ref source, offset)))).AsUInt16();
            return Vector128.WidenLower((samples ^ (samples >>> 8)) & Vector128.Create((ushort)255));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<uint> LoadHashBytes(ref ushort source, nuint offset, Vector256<uint> lanes)
        {
            Vector128<ushort> samples = Vector128.LoadUnsafe(ref source, offset);
            return Vector256.WidenLower(((samples ^ (samples >>> 8)) & Vector128.Create((ushort)255)).ToVector256Unsafe());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<uint> LoadHashBytes(ref ushort source, nuint offset, Vector512<uint> lanes)
        {
            Vector256<ushort> samples = Vector256.LoadUnsafe(ref source, offset);
            return Vector512.WidenLower(((samples ^ (samples >>> 8)) & Vector256.Create((ushort)255)).ToVector512Unsafe());
        }

        /// <inheritdoc/>
        public static bool IsHorizontalPerfect(ReadOnlySpan<ushort> block, int stride)
        {
            for (int row = 0; row < 8; row++)
            {
                ReadOnlySpan<ushort> samples = block[(row * stride)..];
                if (Vector128.IsHardwareAccelerated)
                {
                    Vector128<ushort> vector = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(samples));
                    if (!Vector128.EqualsAll(vector, Vector128.Create(samples[0])))
                    {
                        return false;
                    }
                }
                else if (samples[1..8].ContainsAnyExcept(samples[0]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public static bool IsVerticalPerfect(ReadOnlySpan<ushort> block, int stride)
        {
            // Every column repeats its first sample when every row equals the first row.
            for (int row = 1; row < 8; row++)
            {
                if (!RowsEqual(block, block[(row * stride)..]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public static bool BlocksEqual(ReadOnlySpan<ushort> first, int firstStride, ReadOnlySpan<ushort> second, int secondStride)
        {
            for (int row = 0; row < 8; row++)
            {
                if (!RowsEqual(first[(row * firstStride)..], second[(row * secondStride)..]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Compares the first eight samples of two rows.
        /// </summary>
        private static bool RowsEqual(ReadOnlySpan<ushort> firstRow, ReadOnlySpan<ushort> secondRow)
        {
            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<ushort> firstSamples = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(firstRow));
                Vector128<ushort> secondSamples = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(secondRow));
                return Vector128.EqualsAll(firstSamples, secondSamples);
            }

            return firstRow[..8].SequenceEqual(secondRow[..8]);
        }

        /// <inheritdoc/>
        public static void CopyPaletteSamples(
            ReadOnlySpan<ushort> block,
            int stride,
            int rows,
            int columns,
            Span<short> samples)
        {
            for (int row = 0; row < rows; row++)
            {
                MemoryMarshal.Cast<ushort, short>(block.Slice(row * stride, columns)).CopyTo(samples[(row * columns)..]);
            }
        }

        /// <inheritdoc/>
        public static void PreparePalette(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> paletteColors,
            Av1PlaneRegion<byte> colorIndexMap,
            Span<ushort> prediction,
            int predictionStride,
            Span<short> residual,
            Av1TransformSize transformSize)
        {
            int width = transformSize.GetWidth();
            int height = transformSize.GetHeight();
            Av1PalettePredictor.Predict(
                paletteColors,
                colorIndexMap,
                MemoryMarshal.Cast<ushort, short>(prediction),
                predictionStride,
                width,
                height);

            Av1ResidualBuilder.Subtract(
                source,
                sourceStride,
                prediction,
                predictionStride,
                residual,
                width,
                width,
                height);
        }

        /// <inheritdoc/>
        public static void PrepareChromaFromLuma(
            ReadOnlySpan<ushort> reconstruction,
            int reconstructionStride,
            Span<short> lumaQ3,
            Av1TransformSize transformSize,
            Size lumaExtent,
            bool subsamplingX,
            bool subsamplingY)
            => Av1ChromaFromLumaContext.PrepareBlock(
                MemoryMarshal.Cast<ushort, short>(reconstruction),
                reconstructionStride,
                lumaQ3,
                transformSize,
                lumaExtent,
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
        public static void PrepareFilterIntra(
            Span<int> transformWorkspace,
            ReadOnlySpan<ushort> source,
            int sourceStride,
            Span<ushort> prediction,
            int predictionStride,
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
            Span<short> filterRows = MemoryMarshal.Cast<int, short>(transformWorkspace).Slice(
                0,
                Av1FilterIntraPredictorBase.BufferLength);

            Av1FilterIntraPredictorBase.GetPredictor(filterIntraMode)
                .Predict(
                    MemoryMarshal.Cast<ushort, short>(prediction),
                    predictionStride,
                    MemoryMarshal.Cast<ushort, short>(above),
                    MemoryMarshal.Cast<ushort, short>(left),
                    width,
                    height,
                    bitDepth.GetBitCount(),
                    filterRows);

            Av1ResidualBuilder.Subtract(
                source,
                sourceStride,
                prediction,
                predictionStride,
                residual,
                width,
                width,
                height);
        }

        /// <inheritdoc/>
        public static void PrepareIntraBlockCopyPrediction(
            Av1PlaneRegion<ushort> source,
            ReadOnlySpan<ushort> sourceSamples,
            Point blockOrigin,
            Av1PlaneRegion<ushort> reconstruction,
            ReadOnlySpan<ushort> reconstructionSamples,
            Point predictionOrigin,
            bool halfX,
            bool halfY,
            Span<ushort> prediction,
            Span<short> residual,
            Av1BlockSize predictionSize)
        {
            int width = predictionSize.GetWidth();
            int height = predictionSize.GetHeight();
            Av1IntraBlockCopyPredictor.Predict(
                MemoryMarshal.Cast<ushort, short>(Av1TransformBlockEncoder.GetPlaneSpan(reconstructionSamples, reconstruction, predictionOrigin)),
                reconstruction.Stride,
                MemoryMarshal.Cast<ushort, short>(prediction),
                width,
                width,
                height,
                halfX,
                halfY);

            Av1ResidualBuilder.Subtract(
                Av1TransformBlockEncoder.GetPlaneSpan(sourceSamples, source, blockOrigin),
                source.Stride,
                prediction,
                width,
                residual,
                width,
                width,
                height);
        }

        /// <inheritdoc/>
        public static void PrepareWarpedInterPrediction(
            Av1PlaneRegion<ushort> reference,
            ReadOnlySpan<ushort> referenceSamples,
            int referenceWidth,
            int referenceHeight,
            Point blockOrigin,
            int width,
            int height,
            int subsamplingX,
            int subsamplingY,
            Av1GlobalMotionParameters parameters,
            Span<ushort> prediction,
            Span<short> intermediateTile,
            Av1BitDepth bitDepth)
            => Av1WarpedInterPredictor.PredictWarped(
                referenceSamples,
                reference.Stride,
                reference.Bounds.Location,
                referenceWidth,
                referenceHeight,
                prediction,
                width,
                blockOrigin,
                width,
                height,
                subsamplingX,
                subsamplingY,
                bitDepth.GetBitCount(),
                parameters,
                intermediateTile);

        /// <inheritdoc/>
        public static void PrepareWarpedCompoundIntermediate(
            Av1PlaneRegion<ushort> reference,
            ReadOnlySpan<ushort> referenceSamples,
            int referenceWidth,
            int referenceHeight,
            Point blockOrigin,
            int width,
            int height,
            int subsamplingX,
            int subsamplingY,
            Av1GlobalMotionParameters parameters,
            Span<ushort> intermediate,
            Span<short> intermediateTile,
            Av1BitDepth bitDepth)
            => Av1WarpedInterPredictor.PredictWarpedCompound(
                referenceSamples,
                reference.Stride,
                reference.Bounds.Location,
                referenceWidth,
                referenceHeight,
                intermediate,
                width,
                blockOrigin,
                width,
                height,
                subsamplingX,
                subsamplingY,
                bitDepth.GetBitCount(),
                parameters,
                intermediateTile);

        /// <inheritdoc/>
        public static void BlendMask(
            Span<ushort> destination,
            int destinationStride,
            ReadOnlySpan<ushort> second,
            int secondStride,
            ReadOnlySpan<byte> mask,
            int maskStride,
            int width,
            int height)
            => Av1CompoundMaskBlendPredictor.Blend(destination, destinationStride, second, secondStride, mask, maskStride, width, height);

        /// <inheritdoc/>
        public static void BuildCompoundIntermediate(
            ReadOnlySpan<ushort> reference,
            int referenceStride,
            int referenceOrigin,
            Span<ushort> intermediate,
            Span<short> intermediateRows,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            int bitDepth)
            => Av1CompoundInterPredictor.PredictCompound(
                reference,
                referenceStride,
                referenceOrigin,
                intermediate,
                width,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                bitDepth,
                intermediateRows);

        /// <inheritdoc/>
        public static void AverageCompoundIntermediates(
            Span<ushort> prediction,
            ReadOnlySpan<ushort> first,
            ReadOnlySpan<ushort> second,
            int width,
            int height,
            int bitDepth)
            => Av1CompoundIntermediateAveragePredictor.AverageIntermediate(prediction, width, first, width, second, width, width, height, bitDepth);

        /// <inheritdoc/>
        public static void WeightCompoundIntermediates(
            Span<ushort> prediction,
            ReadOnlySpan<ushort> first,
            ReadOnlySpan<ushort> second,
            int width,
            int height,
            int firstWeight,
            int secondWeight,
            int bitDepth)
            => Av1CompoundIntermediateDistanceWeightedPredictor.DistanceWeightedIntermediate(
                prediction, width, first, width, second, width, width, height, firstWeight, secondWeight, bitDepth);

        /// <inheritdoc/>
        public static void BlendCompoundIntermediates(
            Span<ushort> prediction,
            ReadOnlySpan<ushort> first,
            ReadOnlySpan<ushort> second,
            ReadOnlySpan<byte> mask,
            int maskStride,
            int width,
            int height,
            int subsamplingX,
            int subsamplingY,
            int bitDepth)
            => Av1CompoundIntermediateMaskBlendPredictor.BlendIntermediate(
                prediction, width, first, width, second, width, mask, maskStride, width, height, subsamplingX, subsamplingY, bitDepth);

        /// <inheritdoc/>
        public static void BuildCompoundDifferenceMask(
            Span<byte> mask,
            ReadOnlySpan<ushort> first,
            ReadOnlySpan<ushort> second,
            Size size,
            Av1BitDepth bitDepth,
            Av1DifferenceWeightedMaskType maskType)
            => Av1DifferenceWeightedMaskBuilder.FillDifferenceWeightedMask(
                mask,
                size.Width,
                first,
                size.Width,
                second,
                size.Width,
                size.Width,
                size.Height,
                bitDepth.GetBitCount(),
                maskType);

        /// <inheritdoc/>
        public static void ApplyChromaFromLuma(
            ReadOnlySpan<short> lumaQ3,
            Span<ushort> prediction,
            int predictionStride,
            int alphaQ3,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth)
            => Av1ChromaFromLumaPredictor.Predict(
                lumaQ3,
                MemoryMarshal.Cast<ushort, short>(prediction),
                predictionStride,
                alphaQ3,
                bitDepth.GetBitCount(),
                transformSize.GetWidth(),
                transformSize.GetHeight());

        /// <inheritdoc/>
        public static void AddSelectedResidual(
            ReadOnlySpan<int> dequantizedCoefficients,
            Span<int> transformWorkspace,
            Span<ushort> prediction,
            int stride,
            Av1TransformSize transformSize,
            Av1Plane plane,
            Av1BitDepth bitDepth,
            bool lossless,
            Av1EncoderTransformBlockState state)
            => Av1InverseTransformer.ReconstructHighBitDepth(
                dequantizedCoefficients,
                MemoryMarshal.Cast<ushort, short>(prediction),
                stride,
                transformSize,
                state.TransformType,
                (int)plane,
                state.EndOfBlock,
                lossless,
                bitDepth,
                transformWorkspace);
    }
}
