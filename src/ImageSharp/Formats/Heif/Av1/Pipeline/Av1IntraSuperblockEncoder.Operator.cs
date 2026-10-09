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
        /// Converts a native unsigned sample to an integer for source-domain analysis.
        /// </summary>
        /// <param name="sample">The source sample.</param>
        /// <returns>The sample value.</returns>
        public static abstract int GetSampleValue(TSample sample);

        /// <summary>
        /// Measures how far a block departs from its own 3x3 smoothing. The columns and rows past the visible ones
        /// repeat the last visible column and row. Reference: aom_calc_variance_stat() and
        /// aom_highbd_calc_variance_stat().
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
        /// <param name="prediction">The contiguous prediction destination.</param>
        /// <param name="residual">The contiguous source-minus-prediction destination.</param>
        /// <param name="transformSize">The prediction dimensions.</param>
        public static abstract void PreparePalette(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<ushort> paletteColors,
            Av1PlaneRegion<byte> colorIndexMap,
            Span<TSample> prediction,
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
        /// Builds a compound prediction from two retained reference frames using the selected blend.
        /// </summary>
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
        /// <param name="prediction">The contiguous averaged prediction destination.</param>
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
        public static abstract void PrepareCompoundInterPrediction(
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
            bool secondaryPrepared);

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
        /// Blends a spatial intra predictor into a completed inter predictor through an AV1 alpha mask.
        /// </summary>
        /// <param name="interPrediction">The inter predictor, overwritten by the blended samples.</param>
        /// <param name="intraPrediction">The contiguous intra predictor samples.</param>
        /// <param name="mask">The per-sample intra weights in the inclusive range zero through 64.</param>
        /// <param name="width">The prediction width in samples.</param>
        /// <param name="height">The prediction height in samples.</param>
        public static abstract void BlendInterIntraPrediction(
            Span<TSample> interPrediction,
            ReadOnlySpan<TSample> intraPrediction,
            ReadOnlySpan<byte> mask,
            int width,
            int height);

        /// <summary>
        /// Encodes and reconstructs one DC intra transform block.
        /// </summary>
        /// <param name="blockWorkspace">The reusable block workspace.</param>
        /// <param name="source">The coded source plane.</param>
        /// <param name="sourceSamples">The samples of the complete source plane, read once by the caller.</param>
        /// <param name="reconstruction">The coded reconstruction plane.</param>
        /// <param name="reconstructionSamples">The samples of the complete reconstruction plane, read once by the caller.</param>
        /// <param name="blockOrigin">The transform-block origin in plane samples.</param>
        /// <param name="above">The top reference samples.</param>
        /// <param name="left">The left reference samples.</param>
        /// <param name="hasLeft">Whether the left reference is available.</param>
        /// <param name="hasAbove">Whether the top reference is available.</param>
        /// <param name="quantizedCoefficients">The retained entropy-coding coefficients.</param>
        /// <param name="transformSize">The transform dimensions.</param>
        /// <param name="qIndex">The effective segment quantizer index.</param>
        /// <param name="lossless">Whether the segment of the block codes losslessly.</param>
        /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
        /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
        /// <param name="plane">The component plane containing the block.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        /// <param name="state">The retained transform state.</param>
        public static abstract void Encode(
            Av1EncoderBlockWorkspace blockWorkspace,
            Av1PlaneRegion<TSample> source,
            ReadOnlySpan<TSample> sourceSamples,
            Av1PlaneRegion<TSample> reconstruction,
            Span<TSample> reconstructionSamples,
            Point blockOrigin,
            ReadOnlySpan<TSample> above,
            ReadOnlySpan<TSample> left,
            bool hasLeft,
            bool hasAbove,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            int qIndex,
            bool lossless,
            int dcDeltaQ,
            int acDeltaQ,
            Av1Plane plane,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state);

        /// <summary>
        /// Encodes one intra candidate into contiguous decision scratch.
        /// </summary>
        /// <param name="plane">The values and buffers of the plane that every candidate of the block shares.</param>
        /// <param name="mode">The intra prediction mode.</param>
        /// <param name="angleDelta">The signed directional-angle adjustment.</param>
        /// <param name="transformType">The compound transform applied to the residual.</param>
        /// <param name="state">The candidate transform state.</param>
        /// <param name="sse">The residual energy of leaving the candidate uncoded, measured where its distortion was.</param>
        /// <returns>The normalized distortion in AV1 transform units.</returns>
        public static abstract long EncodeCandidate(
            in Av1IntraCandidatePlane<TSample> plane,
            Av1PredictionMode mode,
            int angleDelta,
            Av1TransformType transformType,
            ref Av1EncoderTransformBlockState state,
            out long sse);

        /// <summary>
        /// Builds one spatial intra prediction and its source residual for reuse across transform candidates.
        /// </summary>
        /// <param name="transformWorkspace">The transform workspace of the block, which serves as edge and prediction scratch.</param>
        /// <param name="source">The source transform block, from its top-left sample.</param>
        /// <param name="sourceStride">The number of samples between rows of <paramref name="source"/>.</param>
        /// <param name="prediction">The prediction destination.</param>
        /// <param name="predictionStride">The distance between prediction rows in samples.</param>
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
            Span<int> transformWorkspace,
            ReadOnlySpan<TSample> source,
            int sourceStride,
            Span<TSample> prediction,
            int predictionStride,
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
        /// <param name="transformWorkspace">The transform workspace of the block, which serves as filter row scratch.</param>
        /// <param name="source">The source transform block, from its top-left sample.</param>
        /// <param name="sourceStride">The number of samples between rows of <paramref name="source"/>.</param>
        /// <param name="prediction">The contiguous prediction destination.</param>
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
        /// Subtracts a retained prediction from its source without rebuilding the inter predictor.
        /// </summary>
        /// <param name="source">The source block, from its top-left sample.</param>
        /// <param name="sourceStride">The number of samples between rows of <paramref name="source"/>.</param>
        /// <param name="prediction">The tightly packed prediction samples.</param>
        /// <param name="residual">The destination signed residual samples.</param>
        /// <param name="width">The plane block width.</param>
        /// <param name="height">The plane block height.</param>
        public static abstract void SubtractPrediction(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> prediction,
            Span<short> residual,
            int width,
            int height);

        /// <summary>
        /// Subtracts one packed prediction from another. Reference: aom_subtract_block and
        /// aom_highbd_subtract_block with both inputs at the block width.
        /// </summary>
        /// <param name="minuend">The prediction to subtract from, packed at the block width.</param>
        /// <param name="subtrahend">The prediction to subtract, packed at the block width.</param>
        /// <param name="difference">The destination signed differences, packed at the block width.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        public static abstract void SubtractPackedPrediction(
            ReadOnlySpan<TSample> minuend,
            ReadOnlySpan<TSample> subtrahend,
            Span<short> difference,
            int width,
            int height);

        /// <summary>
        /// Predicts a block with an affine warped model. Reference: av1_warp_plane(), which
        /// av1_make_inter_predictor() calls for a warped block.
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
        /// <param name="scratch">The warp filter intermediate storage.</param>
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
            Span<short> scratch,
            Av1BitDepth bitDepth);

        /// <summary>
        /// Predicts one reference of a compound block with an affine warped model into the unsigned compound
        /// intermediate. Reference: av1_warp_plane() with the compound convolve parameters that
        /// av1_make_inter_predictor() passes for a warped reference of a compound block.
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
        /// <param name="scratch">The warp filter intermediate storage.</param>
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
            Span<short> scratch,
            Av1BitDepth bitDepth);

        /// <summary>
        /// Builds a translational prediction from a retained reference frame and the matching source residual.
        /// </summary>
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
        public static abstract void PrepareTranslationalInterPrediction(
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
        /// Predicts one translational rectangle into a strided destination, without a residual.
        /// </summary>
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
        public static abstract void PredictTranslationalInter(
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
            Av1BitDepth bitDepth);

        /// <summary>
        /// Predicts one rectangle from a reference of another size than the frame into a strided destination, with
        /// a source position and phase that advance by a step per output sample. Reference: the scaled branch of
        /// av1_make_inter_predictor(), which calls av1_convolve_2d_scale() or av1_highbd_convolve_2d_scale().
        /// </summary>
        /// <param name="reference">The padded retained reference plane.</param>
        /// <param name="referenceSamples">The samples of the complete reference plane, read once by the caller.</param>
        /// <param name="predictionOrigin">The integer reference position of the first output sample.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="horizontalPhase">The horizontal phase of the first output sample in 1/1024 samples.</param>
        /// <param name="horizontalStep">The horizontal step per output sample in 1/1024 samples.</param>
        /// <param name="verticalPhase">The vertical phase of the first output sample in 1/1024 samples.</param>
        /// <param name="verticalStep">The vertical step per output sample in 1/1024 samples.</param>
        /// <param name="prediction">The prediction destination, starting at the rectangle.</param>
        /// <param name="predictionStride">The destination stride.</param>
        /// <param name="width">The rectangle width.</param>
        /// <param name="height">The rectangle height.</param>
        /// <param name="filterRows">The intermediate storage used by two-dimensional filtering.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        public static abstract void PredictScaledInter(
            Av1PlaneRegion<TSample> reference,
            ReadOnlySpan<TSample> referenceSamples,
            Point predictionOrigin,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int horizontalStep,
            int verticalPhase,
            int verticalStep,
            Span<TSample> prediction,
            int predictionStride,
            int width,
            int height,
            Span<short> filterRows,
            Av1BitDepth bitDepth);

        /// <summary>
        /// Applies the selected luma adjustment to a DC-predicted chroma block.
        /// </summary>
        /// <param name="lumaQ3">The zero-mean Q3 luma surface.</param>
        /// <param name="prediction">The DC prediction receiving the adjustment.</param>
        /// <param name="alphaQ3">The signed Q3 scale.</param>
        /// <param name="transformSize">The chroma dimensions.</param>
        /// <param name="bitDepth">The coded sample precision.</param>
        public static abstract void ApplyChromaFromLuma(
            ReadOnlySpan<short> lumaQ3,
            Span<TSample> prediction,
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
        /// Reconstructs a quantized candidate from its dequantized coefficients and measures its distortion.
        /// </summary>
        /// <param name="blockWorkspace">The workspace supplying the visible extent of the plane.</param>
        /// <param name="transformWorkspace">The intermediate buffer of the inverse transform.</param>
        /// <param name="dequantized">The dequantized coefficients of the candidate.</param>
        /// <param name="source">The source transform block, from its top-left sample.</param>
        /// <param name="sourceStride">The number of samples between rows of <paramref name="source"/>.</param>
        /// <param name="blockOrigin">The transform-block origin in plane samples, which gives the visible extent.</param>
        /// <param name="prediction">The prediction samples at the transform origin.</param>
        /// <param name="inputStride">The number of prediction samples between rows.</param>
        /// <param name="reconstruction">The candidate reconstruction.</param>
        /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
        /// <param name="transformSize">The transform dimensions.</param>
        /// <param name="plane">The component plane containing the block.</param>
        /// <param name="lossless">Whether the segment of the block codes losslessly, which selects the reversible inverse transform.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        /// <param name="state">The candidate transform state.</param>
        /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
        public static abstract long ReconstructPredictionCandidate(
            Av1EncoderBlockWorkspace blockWorkspace,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> dequantized,
            ReadOnlySpan<TSample> source,
            int sourceStride,
            Point blockOrigin,
            ReadOnlySpan<TSample> prediction,
            int inputStride,
            Span<TSample> reconstruction,
            int reconstructionStride,
            Av1TransformSize transformSize,
            Av1Plane plane,
            bool lossless,
            Av1BitDepth bitDepth,
            Av1EncoderTransformBlockState state);
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
        public static void BuildPrediction(
            ReadOnlySpan<byte> reference,
            int referenceStride,
            int referenceOrigin,
            Span<byte> prediction,
            Span<short> scratch,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            int bitDepth)
            => Av1MotionSearchBase.ByteOperator.BuildPrediction(
                reference,
                referenceStride,
                referenceOrigin,
                prediction,
                scratch,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                bitDepth);

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
        public static void Subtract(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            Span<short> residual,
            int width,
            int height)
            => Av1MotionSearchBase.ByteOperator.Subtract(source, sourceStride, prediction, residual, width, height);

        /// <inheritdoc/>
        public static void PredictScaled(
            ReadOnlySpan<byte> reference,
            int referenceStride,
            int referenceOrigin,
            Span<byte> buffer,
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
            => Av1MotionSearchBase.ByteOperator.PredictScaled(
                reference,
                referenceStride,
                referenceOrigin,
                buffer,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                horizontalStep,
                verticalPhase,
                verticalStep,
                intermediateRows,
                bitDepth);

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
            => Av1MotionSearchBase.ByteOperator.SumCompoundAbsoluteDifferences(
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
            => Av1MotionSearchBase.ByteOperator.GetCompoundMoments(
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
            => Av1MotionSearchBase.ByteOperator.GetMoments(
                source, sourceStride, prediction, predictionStride, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static int SumObmcAbsoluteDifferences(
            ReadOnlySpan<byte> prediction,
            int predictionStride,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height)
            => Av1MotionSearchBase.ByteOperator.SumObmcAbsoluteDifferences(prediction, predictionStride, weightedSource, mask, width, height);

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
            => Av1MotionSearchBase.ByteOperator.GetObmcMoments(prediction, predictionStride, weightedSource, mask, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static void WeightObmcAbove(ReadOnlySpan<byte> prediction, int weight, Span<int> weightedSource, Span<int> mask, int width)
            => Av1MotionSearchBase.ByteOperator.WeightObmcAbove(prediction, weight, weightedSource, mask, width);

        /// <inheritdoc/>
        public static void WeightObmcLeft(ReadOnlySpan<byte> prediction, ReadOnlySpan<byte> weights, Span<int> weightedSource, Span<int> mask)
            => Av1MotionSearchBase.ByteOperator.WeightObmcLeft(prediction, weights, weightedSource, mask);

        /// <inheritdoc/>
        public static void ScaleObmcTarget(Span<int> weightedSource, Span<int> mask)
            => Av1MotionSearchBase.ByteOperator.ScaleObmcTarget(weightedSource, mask);

        /// <inheritdoc/>
        public static void SubtractObmcSource(ReadOnlySpan<byte> source, Span<int> weightedSource)
            => Av1MotionSearchBase.ByteOperator.SubtractObmcSource(source, weightedSource);

        /// <inheritdoc/>
        public static Span<byte> GetLeftReference(Span<short> residual, int length)
            => MemoryMarshal.AsBytes(residual)[..length];

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
            // Each widened lane accumulates one column from eight rows. Column sums fit in
            // eleven bits, and their total fits in ushort; round once after the complete sum.
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
                source,
                sourceStride,
                prediction,
                width,
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
        public static void Encode(
            Av1EncoderBlockWorkspace blockWorkspace,
            Av1PlaneRegion<byte> source,
            ReadOnlySpan<byte> sourceSamples,
            Av1PlaneRegion<byte> reconstruction,
            Span<byte> reconstructionSamples,
            Point blockOrigin,
            ReadOnlySpan<byte> above,
            ReadOnlySpan<byte> left,
            bool hasLeft,
            bool hasAbove,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            int qIndex,
            bool lossless,
            int dcDeltaQ,
            int acDeltaQ,
            Av1Plane plane,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.EncodeIntraDcLossy(
                blockWorkspace,
                source,
                sourceSamples,
                reconstruction,
                reconstructionSamples,
                blockOrigin,
                above,
                left,
                hasLeft,
                hasAbove,
                quantizedCoefficients,
                transformSize,
                Av1TransformType.DctDct,
                qIndex,
                lossless,
                dcDeltaQ,
                acDeltaQ,
                plane,
                ref state);

        /// <inheritdoc/>
        public static long EncodeCandidate(
            in Av1IntraCandidatePlane<byte> plane,
            Av1PredictionMode mode,
            int angleDelta,
            Av1TransformType transformType,
            ref Av1EncoderTransformBlockState state,
            out long sse)
            => Av1TransformBlockEncoder.EncodeIntraLossyCandidate(in plane, mode, angleDelta, transformType, ref state, out sse);

        /// <inheritdoc/>
        public static void PrepareIntra(
            Span<int> transformWorkspace,
            ReadOnlySpan<byte> source,
            int sourceStride,
            Span<byte> prediction,
            int predictionStride,
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
                transformWorkspace,
                source,
                sourceStride,
                prediction,
                predictionStride,
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
            Span<int> transformWorkspace,
            ReadOnlySpan<byte> source,
            int sourceStride,
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
            Span<byte> filterScratch = MemoryMarshal.AsBytes(transformWorkspace).Slice(
                0,
                Av1FilterIntraPredictorBase.ScratchLength);

            Av1FilterIntraPredictorBase.GetPredictor(filterIntraMode)
                .Predict(prediction, width, above, left, width, height, filterScratch);

            Av1ResidualBuilder.Subtract(
                source,
                sourceStride,
                prediction,
                width,
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
        public static void SubtractPrediction(
            ReadOnlySpan<byte> source,
            int sourceStride,
            ReadOnlySpan<byte> prediction,
            Span<short> residual,
            int width,
            int height)
            => Av1ResidualBuilder.Subtract(
                source,
                sourceStride,
                prediction,
                width,
                residual,
                width,
                width,
                height);

        /// <inheritdoc/>
        public static void SubtractPackedPrediction(
            ReadOnlySpan<byte> minuend,
            ReadOnlySpan<byte> subtrahend,
            Span<short> difference,
            int width,
            int height)
            => Av1ResidualBuilder.Subtract(minuend, width, subtrahend, width, difference, width, width, height);

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
            Span<short> scratch,
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
                scratch);

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
            Span<short> scratch,
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
                scratch);

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
        public static void PredictTranslationalInter(
            Av1PlaneRegion<byte> reference,
            ReadOnlySpan<byte> referenceSamples,
            Point predictionOrigin,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            Span<byte> prediction,
            int predictionStride,
            int width,
            int height,
            Span<short> filterRows,
            Av1BitDepth bitDepth)
        {
            Rectangle referenceBounds = reference.Bounds;
            int referenceOrigin =
                ((referenceBounds.Y + predictionOrigin.Y) * reference.Stride) +
                referenceBounds.X +
                predictionOrigin.X;

            Av1TranslationalInterPredictor.Predict(
                referenceSamples,
                reference.Stride,
                referenceOrigin,
                prediction,
                predictionStride,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                filterRows);
        }

        /// <inheritdoc/>
        public static void PredictScaledInter(
            Av1PlaneRegion<byte> reference,
            ReadOnlySpan<byte> referenceSamples,
            Point predictionOrigin,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int horizontalStep,
            int verticalPhase,
            int verticalStep,
            Span<byte> prediction,
            int predictionStride,
            int width,
            int height,
            Span<short> filterRows,
            Av1BitDepth bitDepth)
        {
            Rectangle referenceBounds = reference.Bounds;
            int referenceOrigin =
                ((referenceBounds.Y + predictionOrigin.Y) * reference.Stride) +
                referenceBounds.X +
                predictionOrigin.X;

            Av1ScaledInterPredictor.PredictScaled(
                referenceSamples,
                reference.Stride,
                referenceOrigin,
                prediction,
                predictionStride,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                horizontalStep,
                verticalPhase,
                verticalStep,
                filterRows);
        }

        /// <inheritdoc/>
        public static void PrepareTranslationalInterPrediction(
            Av1PlaneRegion<byte> source,
            ReadOnlySpan<byte> sourceSamples,
            Point blockOrigin,
            Av1PlaneRegion<byte> reference,
            ReadOnlySpan<byte> referenceSamples,
            Point predictionOrigin,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            Span<byte> prediction,
            Span<short> residual,
            Span<short> filterRows,
            Av1BlockSize predictionSize,
            Av1BitDepth bitDepth)
        {
            int width = predictionSize.GetWidth();
            int height = predictionSize.GetHeight();
            Rectangle referenceBounds = reference.Bounds;
            int referenceOrigin =
                ((referenceBounds.Y + predictionOrigin.Y) * reference.Stride) +
                referenceBounds.X +
                predictionOrigin.X;

            Av1TranslationalInterPredictor.Predict(
                referenceSamples,
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
                filterRows);

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
        public static void PrepareCompoundInterPrediction(
            Av1PlaneRegion<byte> source,
            ReadOnlySpan<byte> sourceSamples,
            Point blockOrigin,
            Av1PlaneRegion<byte> primaryReference,
            ReadOnlySpan<byte> primaryReferenceSamples,
            Point primaryPredictionOrigin,
            int primaryHorizontalPhase,
            int primaryVerticalPhase,
            Av1PlaneRegion<byte> secondaryReference,
            ReadOnlySpan<byte> secondaryReferenceSamples,
            Point secondaryPredictionOrigin,
            int secondaryHorizontalPhase,
            int secondaryVerticalPhase,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            Span<byte> prediction,
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
        {
            int width = predictionSize.GetWidth();
            int height = predictionSize.GetHeight();
            Rectangle primaryBounds = primaryReference.Bounds;
            int primaryOrigin = ((primaryBounds.Y + primaryPredictionOrigin.Y) * primaryReference.Stride) +
                primaryBounds.X + primaryPredictionOrigin.X;

            Rectangle secondaryBounds = secondaryReference.Bounds;
            int secondaryOrigin = ((secondaryBounds.Y + secondaryPredictionOrigin.Y) * secondaryReference.Stride) +
                secondaryBounds.X + secondaryPredictionOrigin.X;

            if (!primaryPrepared)
            {
                Av1CompoundInterPredictor.PredictCompound(
                    primaryReferenceSamples,
                    primaryReference.Stride,
                    primaryOrigin,
                    firstIntermediate,
                    width,
                    width,
                    height,
                    horizontalFilter,
                    verticalFilter,
                    primaryHorizontalPhase,
                    primaryVerticalPhase,
                    filterRows);
            }

            if (!secondaryPrepared)
            {
                Av1CompoundInterPredictor.PredictCompound(
                    secondaryReferenceSamples,
                    secondaryReference.Stride,
                    secondaryOrigin,
                    secondIntermediate,
                    width,
                    width,
                    height,
                    horizontalFilter,
                    verticalFilter,
                    secondaryHorizontalPhase,
                    secondaryVerticalPhase,
                    filterRows);
            }

            int bitCount = bitDepth.GetBitCount();
            switch (compoundType)
            {
                case Av1CompoundType.DistanceWeighted:
                    Av1CompoundIntermediateDistanceWeightedPredictor.DistanceWeightedIntermediate(
                        prediction,
                        width,
                        firstIntermediate,
                        width,
                        secondIntermediate,
                        width,
                        width,
                        height,
                        firstWeight,
                        secondWeight,
                        bitCount);

                    break;
                case Av1CompoundType.Wedge:
                    Av1WedgeMask.Fill(compoundMask, lumaBlockSize.GetWidth(), lumaBlockSize, wedgeIndex, wedgeSign, 0, 0, invert: false);
                    Av1CompoundIntermediateMaskBlendPredictor.BlendIntermediate(
                        prediction,
                        width,
                        firstIntermediate,
                        width,
                        secondIntermediate,
                        width,
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

                    Av1CompoundIntermediateMaskBlendPredictor.BlendIntermediate(
                        prediction,
                        width,
                        firstIntermediate,
                        width,
                        secondIntermediate,
                        width,
                        compoundMask,
                        lumaBlockSize.GetWidth(),
                        width,
                        height,
                        subsamplingX,
                        subsamplingY,
                        bitCount);

                    break;
                default:
                    Av1CompoundIntermediateAveragePredictor.AverageIntermediate(
                        prediction, width, firstIntermediate, width, secondIntermediate, width, width, height, bitCount);

                    break;
            }

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
        public static void BlendInterIntraPrediction(
            Span<byte> interPrediction,
            ReadOnlySpan<byte> intraPrediction,
            ReadOnlySpan<byte> mask,
            int width,
            int height)
            => Av1CompoundMaskBlendPredictor.Blend(
                interPrediction,
                width,
                intraPrediction,
                width,
                mask,
                width,
                width,
                height);

        /// <inheritdoc/>
        public static void ApplyChromaFromLuma(
            ReadOnlySpan<short> lumaQ3,
            Span<byte> prediction,
            int alphaQ3,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth)
            => Av1ChromaFromLumaPredictor.Predict(
                lumaQ3, prediction, transformSize.GetWidth(), alphaQ3, transformSize.GetWidth(), transformSize.GetHeight());

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

        /// <inheritdoc/>
        public static long ReconstructPredictionCandidate(
            Av1EncoderBlockWorkspace blockWorkspace,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> dequantized,
            ReadOnlySpan<byte> source,
            int sourceStride,
            Point blockOrigin,
            ReadOnlySpan<byte> prediction,
            int inputStride,
            Span<byte> reconstruction,
            int reconstructionStride,
            Av1TransformSize transformSize,
            Av1Plane plane,
            bool lossless,
            Av1BitDepth bitDepth,
            Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.ReconstructPredictionLossyCandidate(
                blockWorkspace,
                transformWorkspace,
                dequantized,
                source,
                sourceStride,
                blockOrigin,
                prediction,
                inputStride,
                reconstruction,
                reconstructionStride,
                transformSize,
                lossless,
                plane,
                state);
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
            Span<short> scratch,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            int bitDepth)
            => Av1MotionSearchBase.UInt16Operator.BuildPrediction(
                reference,
                referenceStride,
                referenceOrigin,
                prediction,
                scratch,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                bitDepth);

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
        public static void Subtract(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            Span<short> residual,
            int width,
            int height)
            => Av1MotionSearchBase.UInt16Operator.Subtract(source, sourceStride, prediction, residual, width, height);

        /// <inheritdoc/>
        public static void PredictScaled(
            ReadOnlySpan<ushort> reference,
            int referenceStride,
            int referenceOrigin,
            Span<ushort> buffer,
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
            => Av1MotionSearchBase.UInt16Operator.PredictScaled(
                reference,
                referenceStride,
                referenceOrigin,
                buffer,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                horizontalStep,
                verticalPhase,
                verticalStep,
                intermediateRows,
                bitDepth);

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
            => Av1MotionSearchBase.UInt16Operator.SumCompoundAbsoluteDifferences(
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
            => Av1MotionSearchBase.UInt16Operator.GetCompoundMoments(
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
            => Av1MotionSearchBase.UInt16Operator.GetMoments(
                source, sourceStride, prediction, predictionStride, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static int SumObmcAbsoluteDifferences(
            ReadOnlySpan<ushort> prediction,
            int predictionStride,
            ReadOnlySpan<int> weightedSource,
            ReadOnlySpan<int> mask,
            int width,
            int height)
            => Av1MotionSearchBase.UInt16Operator.SumObmcAbsoluteDifferences(prediction, predictionStride, weightedSource, mask, width, height);

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
            => Av1MotionSearchBase.UInt16Operator.GetObmcMoments(prediction, predictionStride, weightedSource, mask, width, height, out sum, out squares);

        /// <inheritdoc/>
        public static void WeightObmcAbove(ReadOnlySpan<ushort> prediction, int weight, Span<int> weightedSource, Span<int> mask, int width)
            => Av1MotionSearchBase.UInt16Operator.WeightObmcAbove(prediction, weight, weightedSource, mask, width);

        /// <inheritdoc/>
        public static void WeightObmcLeft(ReadOnlySpan<ushort> prediction, ReadOnlySpan<byte> weights, Span<int> weightedSource, Span<int> mask)
            => Av1MotionSearchBase.UInt16Operator.WeightObmcLeft(prediction, weights, weightedSource, mask);

        /// <inheritdoc/>
        public static void ScaleObmcTarget(Span<int> weightedSource, Span<int> mask)
            => Av1MotionSearchBase.UInt16Operator.ScaleObmcTarget(weightedSource, mask);

        /// <inheritdoc/>
        public static void SubtractObmcSource(ReadOnlySpan<ushort> source, Span<int> weightedSource)
            => Av1MotionSearchBase.UInt16Operator.SubtractObmcSource(source, weightedSource);

        /// <inheritdoc/>
        public static Span<ushort> GetLeftReference(Span<short> residual, int length)
            => MemoryMarshal.Cast<short, ushort>(residual)[..length];

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
                source,
                sourceStride,
                prediction,
                width,
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
        public static void Encode(
            Av1EncoderBlockWorkspace blockWorkspace,
            Av1PlaneRegion<ushort> source,
            ReadOnlySpan<ushort> sourceSamples,
            Av1PlaneRegion<ushort> reconstruction,
            Span<ushort> reconstructionSamples,
            Point blockOrigin,
            ReadOnlySpan<ushort> above,
            ReadOnlySpan<ushort> left,
            bool hasLeft,
            bool hasAbove,
            Span<int> quantizedCoefficients,
            Av1TransformSize transformSize,
            int qIndex,
            bool lossless,
            int dcDeltaQ,
            int acDeltaQ,
            Av1Plane plane,
            Av1BitDepth bitDepth,
            ref Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.EncodeIntraDcLossy(
                blockWorkspace,
                source,
                sourceSamples,
                reconstruction,
                reconstructionSamples,
                blockOrigin,
                above,
                left,
                hasLeft,
                hasAbove,
                quantizedCoefficients,
                transformSize,
                Av1TransformType.DctDct,
                qIndex,
                lossless,
                dcDeltaQ,
                acDeltaQ,
                plane,
                bitDepth,
                ref state);

        /// <inheritdoc/>
        public static long EncodeCandidate(
            in Av1IntraCandidatePlane<ushort> plane,
            Av1PredictionMode mode,
            int angleDelta,
            Av1TransformType transformType,
            ref Av1EncoderTransformBlockState state,
            out long sse)
            => Av1TransformBlockEncoder.EncodeIntraLossyCandidate(in plane, mode, angleDelta, transformType, ref state, out sse);

        /// <inheritdoc/>
        public static void PrepareIntra(
            Span<int> transformWorkspace,
            ReadOnlySpan<ushort> source,
            int sourceStride,
            Span<ushort> prediction,
            int predictionStride,
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
                transformWorkspace,
                source,
                sourceStride,
                prediction,
                predictionStride,
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
            Span<int> transformWorkspace,
            ReadOnlySpan<ushort> source,
            int sourceStride,
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
            Span<short> filterScratch = MemoryMarshal.Cast<int, short>(transformWorkspace).Slice(
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
                source,
                sourceStride,
                prediction,
                width,
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
        public static void SubtractPrediction(
            ReadOnlySpan<ushort> source,
            int sourceStride,
            ReadOnlySpan<ushort> prediction,
            Span<short> residual,
            int width,
            int height)
            => Av1ResidualBuilder.Subtract(
                source,
                sourceStride,
                prediction,
                width,
                residual,
                width,
                width,
                height);

        /// <inheritdoc/>
        public static void SubtractPackedPrediction(
            ReadOnlySpan<ushort> minuend,
            ReadOnlySpan<ushort> subtrahend,
            Span<short> difference,
            int width,
            int height)
            => Av1ResidualBuilder.Subtract(minuend, width, subtrahend, width, difference, width, width, height);

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
            Span<short> scratch,
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
                scratch);

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
            Span<short> scratch,
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
                scratch);

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
        public static void PredictTranslationalInter(
            Av1PlaneRegion<ushort> reference,
            ReadOnlySpan<ushort> referenceSamples,
            Point predictionOrigin,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            Span<ushort> prediction,
            int predictionStride,
            int width,
            int height,
            Span<short> filterRows,
            Av1BitDepth bitDepth)
        {
            Rectangle referenceBounds = reference.Bounds;
            int referenceOrigin =
                ((referenceBounds.Y + predictionOrigin.Y) * reference.Stride) +
                referenceBounds.X +
                predictionOrigin.X;

            Av1TranslationalInterPredictor.Predict(
                referenceSamples,
                reference.Stride,
                referenceOrigin,
                prediction,
                predictionStride,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                bitDepth.GetBitCount(),
                filterRows);
        }

        /// <inheritdoc/>
        public static void PredictScaledInter(
            Av1PlaneRegion<ushort> reference,
            ReadOnlySpan<ushort> referenceSamples,
            Point predictionOrigin,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int horizontalStep,
            int verticalPhase,
            int verticalStep,
            Span<ushort> prediction,
            int predictionStride,
            int width,
            int height,
            Span<short> filterRows,
            Av1BitDepth bitDepth)
        {
            Rectangle referenceBounds = reference.Bounds;
            int referenceOrigin =
                ((referenceBounds.Y + predictionOrigin.Y) * reference.Stride) +
                referenceBounds.X +
                predictionOrigin.X;

            Av1ScaledInterPredictor.PredictScaled(
                referenceSamples,
                reference.Stride,
                referenceOrigin,
                prediction,
                predictionStride,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                horizontalStep,
                verticalPhase,
                verticalStep,
                bitDepth.GetBitCount(),
                filterRows);
        }

        /// <inheritdoc/>
        public static void PrepareTranslationalInterPrediction(
            Av1PlaneRegion<ushort> source,
            ReadOnlySpan<ushort> sourceSamples,
            Point blockOrigin,
            Av1PlaneRegion<ushort> reference,
            ReadOnlySpan<ushort> referenceSamples,
            Point predictionOrigin,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            Span<ushort> prediction,
            Span<short> residual,
            Span<short> filterRows,
            Av1BlockSize predictionSize,
            Av1BitDepth bitDepth)
        {
            int width = predictionSize.GetWidth();
            int height = predictionSize.GetHeight();
            Rectangle referenceBounds = reference.Bounds;
            int referenceOrigin =
                ((referenceBounds.Y + predictionOrigin.Y) * reference.Stride) +
                referenceBounds.X +
                predictionOrigin.X;

            Av1TranslationalInterPredictor.Predict(
                referenceSamples,
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
                filterRows);

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
        public static void PrepareCompoundInterPrediction(
            Av1PlaneRegion<ushort> source,
            ReadOnlySpan<ushort> sourceSamples,
            Point blockOrigin,
            Av1PlaneRegion<ushort> primaryReference,
            ReadOnlySpan<ushort> primaryReferenceSamples,
            Point primaryPredictionOrigin,
            int primaryHorizontalPhase,
            int primaryVerticalPhase,
            Av1PlaneRegion<ushort> secondaryReference,
            ReadOnlySpan<ushort> secondaryReferenceSamples,
            Point secondaryPredictionOrigin,
            int secondaryHorizontalPhase,
            int secondaryVerticalPhase,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            Span<ushort> prediction,
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
        {
            int width = predictionSize.GetWidth();
            int height = predictionSize.GetHeight();
            Rectangle primaryBounds = primaryReference.Bounds;
            int primaryOrigin = ((primaryBounds.Y + primaryPredictionOrigin.Y) * primaryReference.Stride) +
                primaryBounds.X + primaryPredictionOrigin.X;

            Rectangle secondaryBounds = secondaryReference.Bounds;
            int secondaryOrigin = ((secondaryBounds.Y + secondaryPredictionOrigin.Y) * secondaryReference.Stride) +
                secondaryBounds.X + secondaryPredictionOrigin.X;

            if (!primaryPrepared)
            {
                Av1CompoundInterPredictor.PredictCompound(
                    primaryReferenceSamples,
                    primaryReference.Stride,
                    primaryOrigin,
                    firstIntermediate,
                    width,
                    width,
                    height,
                    horizontalFilter,
                    verticalFilter,
                    primaryHorizontalPhase,
                    primaryVerticalPhase,
                    bitDepth.GetBitCount(),
                    filterRows);
            }

            if (!secondaryPrepared)
            {
                Av1CompoundInterPredictor.PredictCompound(
                    secondaryReferenceSamples,
                    secondaryReference.Stride,
                    secondaryOrigin,
                    secondIntermediate,
                    width,
                    width,
                    height,
                    horizontalFilter,
                    verticalFilter,
                    secondaryHorizontalPhase,
                    secondaryVerticalPhase,
                    bitDepth.GetBitCount(),
                    filterRows);
            }

            int bitCount = bitDepth.GetBitCount();
            switch (compoundType)
            {
                case Av1CompoundType.DistanceWeighted:
                    Av1CompoundIntermediateDistanceWeightedPredictor.DistanceWeightedIntermediate(
                        prediction,
                        width,
                        firstIntermediate,
                        width,
                        secondIntermediate,
                        width,
                        width,
                        height,
                        firstWeight,
                        secondWeight,
                        bitCount);

                    break;
                case Av1CompoundType.Wedge:
                    Av1WedgeMask.Fill(compoundMask, lumaBlockSize.GetWidth(), lumaBlockSize, wedgeIndex, wedgeSign, 0, 0, invert: false);
                    Av1CompoundIntermediateMaskBlendPredictor.BlendIntermediate(
                        prediction,
                        width,
                        firstIntermediate,
                        width,
                        secondIntermediate,
                        width,
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

                    Av1CompoundIntermediateMaskBlendPredictor.BlendIntermediate(
                        prediction,
                        width,
                        firstIntermediate,
                        width,
                        secondIntermediate,
                        width,
                        compoundMask,
                        lumaBlockSize.GetWidth(),
                        width,
                        height,
                        subsamplingX,
                        subsamplingY,
                        bitCount);

                    break;
                default:
                    Av1CompoundIntermediateAveragePredictor.AverageIntermediate(
                        prediction, width, firstIntermediate, width, secondIntermediate, width, width, height, bitCount);

                    break;
            }

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
        public static void BlendInterIntraPrediction(
            Span<ushort> interPrediction,
            ReadOnlySpan<ushort> intraPrediction,
            ReadOnlySpan<byte> mask,
            int width,
            int height)
            => Av1CompoundMaskBlendPredictor.Blend(
                interPrediction,
                width,
                intraPrediction,
                width,
                mask,
                width,
                width,
                height);

        /// <inheritdoc/>
        public static void ApplyChromaFromLuma(
            ReadOnlySpan<short> lumaQ3,
            Span<ushort> prediction,
            int alphaQ3,
            Av1TransformSize transformSize,
            Av1BitDepth bitDepth)
            => Av1ChromaFromLumaPredictor.Predict(
                lumaQ3,
                MemoryMarshal.Cast<ushort, short>(prediction),
                transformSize.GetWidth(),
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

        /// <inheritdoc/>
        public static long ReconstructPredictionCandidate(
            Av1EncoderBlockWorkspace blockWorkspace,
            Span<int> transformWorkspace,
            ReadOnlySpan<int> dequantized,
            ReadOnlySpan<ushort> source,
            int sourceStride,
            Point blockOrigin,
            ReadOnlySpan<ushort> prediction,
            int inputStride,
            Span<ushort> reconstruction,
            int reconstructionStride,
            Av1TransformSize transformSize,
            Av1Plane plane,
            bool lossless,
            Av1BitDepth bitDepth,
            Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.ReconstructPredictionLossyCandidate(
                blockWorkspace,
                transformWorkspace,
                dequantized,
                source,
                sourceStride,
                blockOrigin,
                prediction,
                inputStride,
                reconstruction,
                reconstructionStride,
                transformSize,
                lossless,
                plane,
                bitDepth,
                state);
    }
}
