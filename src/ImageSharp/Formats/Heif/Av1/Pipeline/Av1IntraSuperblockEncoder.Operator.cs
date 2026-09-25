// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
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
        /// Gets the rounded average of a four-by-four source block.
        /// </summary>
        /// <param name="source">The coded source plane.</param>
        /// <param name="origin">The source block origin.</param>
        /// <returns>The average in the source sample precision.</returns>
        public static abstract int GetAverage4x4(Buffer2DRegion<TSample> source, Point origin);

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
        /// <param name="lumaExtent">The luma samples the encoder coded for this block.</param>
        /// <param name="subsamplingX">Whether luma is subsampled horizontally for chroma.</param>
        /// <param name="subsamplingY">Whether luma is subsampled vertically for chroma.</param>
        public static abstract void PrepareChromaFromLuma(
            Buffer2DRegion<TSample> reconstruction,
            Point blockOrigin,
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
        /// <param name="blockOrigin">The destination block origin in plane samples.</param>
        /// <param name="primaryReference">The padded primary retained reference plane.</param>
        /// <param name="primaryPredictionOrigin">The integer primary-reference origin preceding the subpixel phase.</param>
        /// <param name="primaryHorizontalPhase">The primary horizontal phase in one-sixteenth-sample units.</param>
        /// <param name="primaryVerticalPhase">The primary vertical phase in one-sixteenth-sample units.</param>
        /// <param name="secondaryReference">The padded secondary retained reference plane.</param>
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
        /// <param name="predictionScratch">The intermediate storage used by two-dimensional filtering.</param>
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
        public static abstract void PrepareCompoundInterPrediction(
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            Buffer2DRegion<TSample> primaryReference,
            Point primaryPredictionOrigin,
            int primaryHorizontalPhase,
            int primaryVerticalPhase,
            Buffer2DRegion<TSample> secondaryReference,
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
            Span<short> predictionScratch,
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
            Av1DifferenceWeightedMaskType differenceWeightedMaskType);

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
        /// <param name="writer">The coefficient entropy costs.</param>
        /// <param name="context">The neighboring coefficient contexts.</param>
        /// <param name="rateMultiplier">The rate-distortion multiplier.</param>
        /// <param name="useChromaWeights">Whether chroma uses its own coefficient refinement weights.</param>
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
        /// <param name="distortionPolicy">The transform-domain distortion type and its mean-error threshold.</param>
        /// <param name="state">The candidate transform state.</param>
        /// <param name="sse">The residual energy of leaving the candidate uncoded, measured where its distortion was. Reference: the sse of search_tx_type().</param>
        /// <returns>The normalized distortion in AV1 transform units.</returns>
        public static abstract long EncodeCandidate(
            Av1EncoderBlockWorkspace workspace,
            Av1SymbolEncoder writer,
            Av1TransformBlockContext context,
            int rateMultiplier,
            bool useChromaWeights,
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
            (int Type, uint Threshold) distortionPolicy,
            ref Av1EncoderTransformBlockState state,
            out long sse);

        /// <summary>
        /// Builds one spatial intra prediction and its source residual for reuse across transform candidates.
        /// </summary>
        /// <param name="workspace">The reusable block workspace.</param>
        /// <param name="source">The coded source plane.</param>
        /// <param name="blockOrigin">The transform-block origin in plane samples.</param>
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
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
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
        /// <param name="predictionSize">The prediction dimensions.</param>
        public static abstract void PrepareIntraBlockCopyPrediction(
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            Buffer2DRegion<TSample> reconstruction,
            Point predictionOrigin,
            bool halfX,
            bool halfY,
            Span<TSample> prediction,
            Span<short> residual,
            Av1BlockSize predictionSize);

        /// <summary>
        /// Subtracts a retained prediction from its source without rebuilding the inter predictor.
        /// </summary>
        /// <param name="source">The source plane.</param>
        /// <param name="blockOrigin">The block origin in plane samples.</param>
        /// <param name="prediction">The tightly packed prediction samples.</param>
        /// <param name="residual">The destination signed residual samples.</param>
        /// <param name="width">The plane block width.</param>
        /// <param name="height">The plane block height.</param>
        public static abstract void SubtractPrediction(
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            ReadOnlySpan<TSample> prediction,
            Span<short> residual,
            int width,
            int height);

        /// <summary>
        /// Predicts a block with an affine warped model. Reference: av1_warp_plane(), which
        /// av1_make_inter_predictor() calls for a warped block.
        /// </summary>
        /// <param name="reference">The padded retained reference plane.</param>
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
            Buffer2DRegion<TSample> reference,
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
        /// <param name="predictionSize">The prediction dimensions.</param>
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
        /// <param name="predictionOrigin">The integer reference origin preceding the subpixel phase.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="horizontalPhase">The horizontal phase in one-sixteenth-sample units.</param>
        /// <param name="verticalPhase">The vertical phase in one-sixteenth-sample units.</param>
        /// <param name="prediction">The prediction destination, starting at the rectangle.</param>
        /// <param name="predictionStride">The destination stride.</param>
        /// <param name="width">The rectangle width.</param>
        /// <param name="height">The rectangle height.</param>
        /// <param name="predictionScratch">The intermediate storage used by two-dimensional filtering.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        public static abstract void PredictTranslationalInter(
            Buffer2DRegion<TSample> reference,
            Point predictionOrigin,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            Span<TSample> prediction,
            int predictionStride,
            int width,
            int height,
            Span<short> predictionScratch,
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
        /// <param name="workspace">The dequantized coefficients and transform workspace.</param>
        /// <param name="prediction">The destination starting at the transform origin.</param>
        /// <param name="stride">The destination row stride in samples.</param>
        /// <param name="transformSize">The transform dimensions.</param>
        /// <param name="plane">The component being reconstructed.</param>
        /// <param name="bitDepth">The coded sample precision.</param>
        /// <param name="lossless">Whether the reversible transform is required.</param>
        /// <param name="state">The nonempty transform's type and end-of-block position.</param>
        public static abstract void AddSelectedResidual(
            Av1EncoderBlockWorkspace workspace,
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
        /// <param name="workspace">The workspace supplying transform scratch storage.</param>
        /// <param name="dequantized">The dequantized coefficients of the candidate.</param>
        /// <param name="source">The coded source plane.</param>
        /// <param name="blockOrigin">The transform-block origin in plane samples.</param>
        /// <param name="prediction">The prediction samples at the transform origin.</param>
        /// <param name="inputStride">The number of prediction samples between rows.</param>
        /// <param name="reconstruction">The candidate reconstruction.</param>
        /// <param name="reconstructionStride">The number of reconstruction samples between rows.</param>
        /// <param name="transformSize">The transform dimensions.</param>
        /// <param name="plane">The component plane containing the block.</param>
        /// <param name="qIndex">The effective segment quantizer index.</param>
        /// <param name="bitDepth">The coded sample bit depth.</param>
        /// <param name="state">The candidate transform state.</param>
        /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
        public static abstract long ReconstructPredictionCandidate(
            Av1EncoderBlockWorkspace workspace,
            ReadOnlySpan<int> dequantized,
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            ReadOnlySpan<TSample> prediction,
            int inputStride,
            Span<TSample> reconstruction,
            int reconstructionStride,
            Av1TransformSize transformSize,
            Av1Plane plane,
            int qIndex,
            Av1BitDepth bitDepth,
            in Av1EncoderTransformBlockState state);

        /// <summary>
        /// Encodes one prepared prediction with the selected transform into decision scratch.
        /// </summary>
        /// <param name="workspace">The reusable block workspace.</param>
        /// <param name="writer">The coefficient entropy costs.</param>
        /// <param name="context">The neighboring coefficient contexts.</param>
        /// <param name="rateMultiplier">The block rate-distortion multiplier.</param>
        /// <param name="isInter">Whether the prediction uses an inter transform set.</param>
        /// <param name="useChromaWeights">Whether chroma uses its own coefficient refinement weights.</param>
        /// <param name="source">The coded source plane.</param>
        /// <param name="blockOrigin">The transform-block origin in plane samples.</param>
        /// <param name="prediction">The prediction samples at the transform origin.</param>
        /// <param name="residual">The source-minus-prediction samples at the transform origin.</param>
        /// <param name="inputStride">The number of prediction and residual samples between rows.</param>
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
        /// <param name="sse">The residual energy of leaving the candidate uncoded, measured where its distortion was. Reference: the sse of search_tx_type().</param>
        /// <returns>The normalized pixel-domain distortion in AV1 transform units.</returns>
        public static abstract long EncodePredictionCandidate(
            Av1EncoderBlockWorkspace workspace,
            Av1SymbolEncoder writer,
            Av1TransformBlockContext context,
            int rateMultiplier,
            bool isInter,
            bool useChromaWeights,
            Buffer2DRegion<TSample> source,
            Point blockOrigin,
            ReadOnlySpan<TSample> prediction,
            Span<short> residual,
            int inputStride,
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
            ref Av1EncoderTransformBlockState state,
            out long sse);

        /// <summary>
        /// Encodes one chroma-from-luma candidate into contiguous decision scratch.
        /// </summary>
        /// <param name="workspace">The reusable block workspace.</param>
        /// <param name="writer">The coefficient entropy costs.</param>
        /// <param name="context">The neighboring coefficient contexts.</param>
        /// <param name="rateMultiplier">The rate-distortion multiplier.</param>
        /// <param name="useChromaWeights">Whether chroma uses its own coefficient refinement weights.</param>
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
            Av1SymbolEncoder writer,
            Av1TransformBlockContext context,
            int rateMultiplier,
            bool useChromaWeights,
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
        public static Span<byte> GetLeftReference(Span<short> residual, int length)
            => MemoryMarshal.AsBytes(residual)[..length];

        /// <inheritdoc/>
        public static byte CreateSample(int value) => (byte)value;

        /// <inheritdoc/>
        public static int GetSampleValue(byte sample) => sample;

        /// <inheritdoc/>
        public static int GetAverage4x4(Buffer2DRegion<byte> source, Point origin)
        {
            // Four packed rows occupy sixteen byte lanes. Widen before summing to retain all eight sample bits.
            Vector128<byte> samples = Vector128.Create(
                MemoryMarshal.Read<uint>(source.DangerousGetRowSpan(origin.Y).Slice(origin.X, 4)),
                MemoryMarshal.Read<uint>(source.DangerousGetRowSpan(origin.Y + 1).Slice(origin.X, 4)),
                MemoryMarshal.Read<uint>(source.DangerousGetRowSpan(origin.Y + 2).Slice(origin.X, 4)),
                MemoryMarshal.Read<uint>(source.DangerousGetRowSpan(origin.Y + 3).Slice(origin.X, 4))).AsByte();

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
            Size lumaExtent,
            bool subsamplingX,
            bool subsamplingY)
            => Av1ChromaFromLumaContext.PrepareBlock(
                Av1TransformBlockEncoder.GetPlaneSpan(reconstruction, blockOrigin),
                reconstruction.Stride,
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
            Av1SymbolEncoder writer,
            Av1TransformBlockContext context,
            int rateMultiplier,
            bool useChromaWeights,
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
            (int Type, uint Threshold) distortionPolicy,
            ref Av1EncoderTransformBlockState state,
            out long sse)
            => Av1TransformBlockEncoder.EncodeIntraLossyCandidate(
                workspace,
                writer,
                context,
                rateMultiplier,
                useChromaWeights,
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
                distortionPolicy,
                ref state,
                out sse);

        /// <inheritdoc/>
        public static void PrepareIntra(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<byte> source,
            Point blockOrigin,
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
                workspace,
                Av1TransformBlockEncoder.GetPlaneSpan(source, blockOrigin),
                source.Stride,
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
            Av1BlockSize predictionSize)
        {
            int width = predictionSize.GetWidth();
            int height = predictionSize.GetHeight();
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
            int width,
            int height)
            => Av1ResidualBuilder.Subtract(
                Av1TransformBlockEncoder.GetPlaneSpan(source, blockOrigin),
                source.Stride,
                prediction,
                width,
                residual,
                width,
                width,
                height);

        /// <inheritdoc/>
        public static void PrepareWarpedInterPrediction(
            Buffer2DRegion<byte> reference,
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
                reference.Buffer.DangerousGetSingleSpan(),
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
            Buffer2DRegion<byte> reference,
            Point predictionOrigin,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            Span<byte> prediction,
            int predictionStride,
            int width,
            int height,
            Span<short> predictionScratch,
            Av1BitDepth bitDepth)
        {
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
                predictionStride,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                predictionScratch);
        }

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
        public static void PrepareCompoundInterPrediction(
            Buffer2DRegion<byte> source,
            Point blockOrigin,
            Buffer2DRegion<byte> primaryReference,
            Point primaryPredictionOrigin,
            int primaryHorizontalPhase,
            int primaryVerticalPhase,
            Buffer2DRegion<byte> secondaryReference,
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
            Span<short> predictionScratch,
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
            Av1DifferenceWeightedMaskType differenceWeightedMaskType)
        {
            int width = predictionSize.GetWidth();
            int height = predictionSize.GetHeight();
            Rectangle primaryBounds = primaryReference.Bounds;
            int primaryOrigin = ((primaryBounds.Y + primaryPredictionOrigin.Y) * primaryReference.Stride) +
                primaryBounds.X + primaryPredictionOrigin.X;
            Rectangle secondaryBounds = secondaryReference.Bounds;
            int secondaryOrigin = ((secondaryBounds.Y + secondaryPredictionOrigin.Y) * secondaryReference.Stride) +
                secondaryBounds.X + secondaryPredictionOrigin.X;

            Av1CompoundInterPredictor.PredictCompound(
                primaryReference.Buffer.DangerousGetSingleSpan(),
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
                predictionScratch);

            Av1CompoundInterPredictor.PredictCompound(
                secondaryReference.Buffer.DangerousGetSingleSpan(),
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
                predictionScratch);

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
            Av1EncoderBlockWorkspace workspace,
            Span<byte> prediction,
            int stride,
            Av1TransformSize transformSize,
            Av1Plane plane,
            Av1BitDepth bitDepth,
            bool lossless,
            Av1EncoderTransformBlockState state)
            => Av1InverseTransformer.Reconstruct8Bit(
                workspace.DequantizedCoefficients,
                prediction,
                stride,
                transformSize,
                state.TransformType,
                (int)plane,
                state.EndOfBlock,
                lossless,
                workspace.TransformWorkspace);

        /// <inheritdoc/>
        public static long EncodePredictionCandidate(
            Av1EncoderBlockWorkspace workspace,
            Av1SymbolEncoder writer,
            Av1TransformBlockContext context,
            int rateMultiplier,
            bool isInter,
            bool useChromaWeights,
            Buffer2DRegion<byte> source,
            Point blockOrigin,
            ReadOnlySpan<byte> prediction,
            Span<short> residual,
            int inputStride,
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
            ref Av1EncoderTransformBlockState state,
            out long sse)
            => Av1TransformBlockEncoder.EncodePredictionLossyCandidate(
                workspace,
                writer,
                context,
                rateMultiplier,
                isInter,
                useChromaWeights,
                source,
                blockOrigin,
                prediction,
                residual,
                inputStride,
                reconstruction,
                reconstructionStride,
                quantizedCoefficients,
                transformSize,
                transformType,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                plane,
                ref state,
                out sse);

        /// <inheritdoc/>
        public static long ReconstructPredictionCandidate(
            Av1EncoderBlockWorkspace workspace,
            ReadOnlySpan<int> dequantized,
            Buffer2DRegion<byte> source,
            Point blockOrigin,
            ReadOnlySpan<byte> prediction,
            int inputStride,
            Span<byte> reconstruction,
            int reconstructionStride,
            Av1TransformSize transformSize,
            Av1Plane plane,
            int qIndex,
            Av1BitDepth bitDepth,
            in Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.ReconstructPredictionLossyCandidate(
                workspace,
                dequantized,
                source,
                blockOrigin,
                prediction,
                inputStride,
                reconstruction,
                reconstructionStride,
                transformSize,
                qIndex,
                plane,
                in state);

        /// <inheritdoc/>
        public static long EncodeChromaFromLumaCandidate(
            Av1EncoderBlockWorkspace workspace,
            Av1SymbolEncoder writer,
            Av1TransformBlockContext context,
            int rateMultiplier,
            bool useChromaWeights,
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
                writer,
                context,
                rateMultiplier,
                useChromaWeights,
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
        public static Span<ushort> GetLeftReference(Span<short> residual, int length)
            => MemoryMarshal.Cast<short, ushort>(residual)[..length];

        /// <inheritdoc/>
        public static ushort CreateSample(int value) => (ushort)value;

        /// <inheritdoc/>
        public static int GetSampleValue(ushort sample) => sample;

        /// <inheritdoc/>
        public static int GetAverage4x4(Buffer2DRegion<ushort> source, Point origin)
        {
            // Four ushort lanes accumulate matching columns. Twelve-bit samples keep both column and final sums within ushort.
            Vector64<ushort> columns = Vector64.LoadUnsafe(ref source.DangerousGetRowSpan(origin.Y)[origin.X]) +
                Vector64.LoadUnsafe(ref source.DangerousGetRowSpan(origin.Y + 1)[origin.X]) +
                Vector64.LoadUnsafe(ref source.DangerousGetRowSpan(origin.Y + 2)[origin.X]) +
                Vector64.LoadUnsafe(ref source.DangerousGetRowSpan(origin.Y + 3)[origin.X]);

            return (Vector64.Sum(columns) + 8) >> 4;
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
            Size lumaExtent,
            bool subsamplingX,
            bool subsamplingY)
            => Av1ChromaFromLumaContext.PrepareBlock(
                MemoryMarshal.Cast<ushort, short>(Av1TransformBlockEncoder.GetPlaneSpan(reconstruction, blockOrigin)),
                reconstruction.Stride,
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
            Av1SymbolEncoder writer,
            Av1TransformBlockContext context,
            int rateMultiplier,
            bool useChromaWeights,
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
            (int Type, uint Threshold) distortionPolicy,
            ref Av1EncoderTransformBlockState state,
            out long sse)
            => Av1TransformBlockEncoder.EncodeIntraLossyCandidate(
                workspace,
                writer,
                context,
                rateMultiplier,
                useChromaWeights,
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
                distortionPolicy,
                ref state,
                out sse);

        /// <inheritdoc/>
        public static void PrepareIntra(
            Av1EncoderBlockWorkspace workspace,
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
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
                workspace,
                Av1TransformBlockEncoder.GetPlaneSpan(source, blockOrigin),
                source.Stride,
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
            Av1BlockSize predictionSize)
        {
            int width = predictionSize.GetWidth();
            int height = predictionSize.GetHeight();
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
            int width,
            int height)
            => Av1ResidualBuilder.Subtract(
                Av1TransformBlockEncoder.GetPlaneSpan(source, blockOrigin),
                source.Stride,
                prediction,
                width,
                residual,
                width,
                width,
                height);

        /// <inheritdoc/>
        public static void PrepareWarpedInterPrediction(
            Buffer2DRegion<ushort> reference,
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
                reference.Buffer.DangerousGetSingleSpan(),
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
            Buffer2DRegion<ushort> reference,
            Point predictionOrigin,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            Span<ushort> prediction,
            int predictionStride,
            int width,
            int height,
            Span<short> predictionScratch,
            Av1BitDepth bitDepth)
        {
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
                predictionStride,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                horizontalPhase,
                verticalPhase,
                bitDepth.GetBitCount(),
                predictionScratch);
        }

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
        public static void PrepareCompoundInterPrediction(
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
            Buffer2DRegion<ushort> primaryReference,
            Point primaryPredictionOrigin,
            int primaryHorizontalPhase,
            int primaryVerticalPhase,
            Buffer2DRegion<ushort> secondaryReference,
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
            Span<short> predictionScratch,
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
            Av1DifferenceWeightedMaskType differenceWeightedMaskType)
        {
            int width = predictionSize.GetWidth();
            int height = predictionSize.GetHeight();
            Rectangle primaryBounds = primaryReference.Bounds;
            int primaryOrigin = ((primaryBounds.Y + primaryPredictionOrigin.Y) * primaryReference.Stride) +
                primaryBounds.X + primaryPredictionOrigin.X;
            Rectangle secondaryBounds = secondaryReference.Bounds;
            int secondaryOrigin = ((secondaryBounds.Y + secondaryPredictionOrigin.Y) * secondaryReference.Stride) +
                secondaryBounds.X + secondaryPredictionOrigin.X;

            Av1CompoundInterPredictor.PredictCompound(
                primaryReference.Buffer.DangerousGetSingleSpan(),
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
                predictionScratch);

            Av1CompoundInterPredictor.PredictCompound(
                secondaryReference.Buffer.DangerousGetSingleSpan(),
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
                predictionScratch);

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
            Av1EncoderBlockWorkspace workspace,
            Span<ushort> prediction,
            int stride,
            Av1TransformSize transformSize,
            Av1Plane plane,
            Av1BitDepth bitDepth,
            bool lossless,
            Av1EncoderTransformBlockState state)
            => Av1InverseTransformer.ReconstructHighBitDepth(
                workspace.DequantizedCoefficients,
                MemoryMarshal.Cast<ushort, short>(prediction),
                stride,
                transformSize,
                state.TransformType,
                (int)plane,
                state.EndOfBlock,
                lossless,
                bitDepth,
                workspace.TransformWorkspace);

        /// <inheritdoc/>
        public static long EncodePredictionCandidate(
            Av1EncoderBlockWorkspace workspace,
            Av1SymbolEncoder writer,
            Av1TransformBlockContext context,
            int rateMultiplier,
            bool isInter,
            bool useChromaWeights,
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
            ReadOnlySpan<ushort> prediction,
            Span<short> residual,
            int inputStride,
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
            ref Av1EncoderTransformBlockState state,
            out long sse)
            => Av1TransformBlockEncoder.EncodePredictionLossyCandidate(
                workspace,
                writer,
                context,
                rateMultiplier,
                isInter,
                useChromaWeights,
                source,
                blockOrigin,
                prediction,
                residual,
                inputStride,
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
                ref state,
                out sse);

        /// <inheritdoc/>
        public static long ReconstructPredictionCandidate(
            Av1EncoderBlockWorkspace workspace,
            ReadOnlySpan<int> dequantized,
            Buffer2DRegion<ushort> source,
            Point blockOrigin,
            ReadOnlySpan<ushort> prediction,
            int inputStride,
            Span<ushort> reconstruction,
            int reconstructionStride,
            Av1TransformSize transformSize,
            Av1Plane plane,
            int qIndex,
            Av1BitDepth bitDepth,
            in Av1EncoderTransformBlockState state)
            => Av1TransformBlockEncoder.ReconstructPredictionLossyCandidate(
                workspace,
                dequantized,
                source,
                blockOrigin,
                prediction,
                inputStride,
                reconstruction,
                reconstructionStride,
                transformSize,
                qIndex,
                plane,
                bitDepth,
                in state);

        /// <inheritdoc/>
        public static long EncodeChromaFromLumaCandidate(
            Av1EncoderBlockWorkspace workspace,
            Av1SymbolEncoder writer,
            Av1TransformBlockContext context,
            int rateMultiplier,
            bool useChromaWeights,
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
                writer,
                context,
                rateMultiplier,
                useChromaWeights,
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
