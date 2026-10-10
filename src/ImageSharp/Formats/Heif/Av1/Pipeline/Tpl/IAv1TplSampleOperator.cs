// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// Binds the first pass and the temporal dependency model to the sample-type overloads of the shared measure, prediction,
/// residual and reconstruction kernels. Every member that is not a load or a conversion selects an existing SIMD operator
/// traversal for the sample type. The interface only closes the sample type.
/// </summary>
/// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
internal interface IAv1TplSampleOperator<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// Creates a sample from its value.
    /// </summary>
    /// <param name="value">The sample value.</param>
    /// <returns>The sample.</returns>
    public static abstract TSample CreateSample(int value);

    /// <summary>
    /// Returns the value of a sample.
    /// </summary>
    /// <param name="sample">The sample.</param>
    /// <returns>The sample value at native precision.</returns>
    public static abstract int ToInt32(TSample sample);

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
    /// Measures the raw absolute differences of every row of a block, before any high-bit-depth precision shift.
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
    /// </summary>
    /// <param name="source">The source samples at the block origin.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="reference">The reference samples at the displaced block origin.</param>
    /// <param name="referenceStride">The reference row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="sum">Receives the raw signed difference sum.</param>
    /// <param name="squares">Receives the raw squared difference sum.</param>
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
    /// Subtracts a prediction from its source into a residual.
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
    /// Predicts a block from the mean of its prepared edges. With one edge available, the mean uses that edge only. With no edge
    /// available, the prediction is the mid-range value.
    /// </summary>
    /// <param name="hasLeft">Whether left samples are available.</param>
    /// <param name="hasAbove">Whether above samples are available.</param>
    /// <param name="destination">The prediction destination.</param>
    /// <param name="stride">The destination row stride.</param>
    /// <param name="above">The prepared above edge.</param>
    /// <param name="left">The prepared left edge.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="bitDepth">The sample precision.</param>
    public static abstract void PredictDc(
        bool hasLeft,
        bool hasAbove,
        Span<TSample> destination,
        int stride,
        ReadOnlySpan<TSample> above,
        ReadOnlySpan<TSample> left,
        int width,
        int height,
        int bitDepth);

    /// <summary>
    /// Predicts a smooth or Paeth block from prepared edges.
    /// </summary>
    /// <param name="mode">The smooth, smooth-vertical, smooth-horizontal or Paeth mode.</param>
    /// <param name="destination">The prediction destination.</param>
    /// <param name="stride">The destination row stride.</param>
    /// <param name="above">The prepared above edge with its corner before it.</param>
    /// <param name="left">The prepared left edge with its corner before it.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    public static abstract void PredictNonDirectional(
        Av1PredictionMode mode,
        Span<TSample> destination,
        int stride,
        ReadOnlySpan<TSample> above,
        ReadOnlySpan<TSample> left,
        int width,
        int height);

    /// <summary>
    /// Filters and upsamples prepared directional edges.
    /// </summary>
    /// <param name="above">The above edge with its corner and prefix before it.</param>
    /// <param name="left">The left edge with its corner and prefix before it.</param>
    /// <param name="width">The transform width.</param>
    /// <param name="height">The transform height.</param>
    /// <param name="angle">The prediction angle.</param>
    /// <param name="topCount">The number of available above samples.</param>
    /// <param name="leftCount">The number of available left samples.</param>
    /// <param name="filterType">Whether a neighbor uses a smooth mode.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <param name="originalEdge">The edge filter workspace.</param>
    /// <param name="upsampleAbove">Receives whether the above edge was upsampled.</param>
    /// <param name="upsampleLeft">Receives whether the left edge was upsampled.</param>
    public static abstract void PrepareDirectionalEdges(
        Span<TSample> above,
        Span<TSample> left,
        int width,
        int height,
        int angle,
        int topCount,
        int leftCount,
        bool filterType,
        int bitDepth,
        Span<TSample> originalEdge,
        out bool upsampleAbove,
        out bool upsampleLeft);

    /// <summary>
    /// Predicts a directional block from prepared edges.
    /// </summary>
    /// <param name="destination">The prediction destination.</param>
    /// <param name="stride">The destination row stride.</param>
    /// <param name="transformSize">The block dimensions.</param>
    /// <param name="above">The prepared above edge.</param>
    /// <param name="left">The prepared left edge.</param>
    /// <param name="upsampleAbove">Whether the above edge is upsampled.</param>
    /// <param name="upsampleLeft">Whether the left edge is upsampled.</param>
    /// <param name="angle">The prediction angle.</param>
    /// <param name="transposedBlock">The predictor workspace of at least one block.</param>
    public static abstract void PredictDirectional(
        Span<TSample> destination,
        int stride,
        Av1TransformSize transformSize,
        ReadOnlySpan<TSample> above,
        ReadOnlySpan<TSample> left,
        bool upsampleAbove,
        bool upsampleLeft,
        int angle,
        Span<TSample> transposedBlock);

    /// <summary>
    /// Predicts one reference with the regular eight-tap filter, rounded to samples.
    /// </summary>
    /// <param name="reference">The complete bordered reference plane.</param>
    /// <param name="referenceStride">The reference row stride.</param>
    /// <param name="referenceOrigin">The index of the integer-position sample.</param>
    /// <param name="destination">The prediction destination.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="horizontalPhase">The horizontal phase in sixteenth samples.</param>
    /// <param name="verticalPhase">The vertical phase in sixteenth samples.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <param name="intermediateRows">The convolution workspace.</param>
    public static abstract void PredictTranslational(
        ReadOnlySpan<TSample> reference,
        int referenceStride,
        int referenceOrigin,
        Span<TSample> destination,
        int destinationStride,
        int width,
        int height,
        int horizontalPhase,
        int verticalPhase,
        int bitDepth,
        Span<short> intermediateRows);

    /// <summary>
    /// Predicts one reference of a compound pair into the unrounded compound intermediate with the regular filter.
    /// </summary>
    /// <param name="reference">The complete bordered reference plane.</param>
    /// <param name="referenceStride">The reference row stride.</param>
    /// <param name="referenceOrigin">The index of the integer-position sample.</param>
    /// <param name="destination">The intermediate destination.</param>
    /// <param name="destinationStride">The intermediate row stride.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="horizontalPhase">The horizontal phase in sixteenth samples.</param>
    /// <param name="verticalPhase">The vertical phase in sixteenth samples.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <param name="intermediateRows">The convolution workspace.</param>
    public static abstract void PredictCompoundIntermediate(
        ReadOnlySpan<TSample> reference,
        int referenceStride,
        int referenceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        int horizontalPhase,
        int verticalPhase,
        int bitDepth,
        Span<short> intermediateRows);

    /// <summary>
    /// Averages two compound intermediates with equal weights into rounded samples.
    /// </summary>
    /// <param name="destination">The prediction destination.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="first">The first intermediate.</param>
    /// <param name="second">The second intermediate.</param>
    /// <param name="width">The block width, also the intermediate stride.</param>
    /// <param name="height">The block height.</param>
    /// <param name="bitDepth">The sample precision.</param>
    public static abstract void AverageCompound(
        Span<TSample> destination,
        int destinationStride,
        ReadOnlySpan<ushort> first,
        ReadOnlySpan<ushort> second,
        int width,
        int height,
        int bitDepth);

    /// <summary>
    /// Adds the inverse two-dimensional DCT of dequantized coefficients to a prediction in place.
    /// </summary>
    /// <param name="coefficients">The dequantized coefficients.</param>
    /// <param name="destination">The prediction, replaced by the reconstruction.</param>
    /// <param name="stride">The destination row stride.</param>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="endOfBlock">The end of block.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <param name="workspace">The inverse transform workspace.</param>
    public static abstract void Reconstruct(
        ReadOnlySpan<int> coefficients,
        Span<TSample> destination,
        int stride,
        Av1TransformSize transformSize,
        int endOfBlock,
        Av1BitDepth bitDepth,
        Span<int> workspace);

    /// <summary>
    /// Classifies a key frame for screen-content tools with the same detection that the good-quality frame
    /// encoder uses, so the tune selects the detection.
    /// </summary>
    /// <param name="source">The coded source frame.</param>
    /// <param name="speed">The good-quality speed.</param>
    /// <param name="tuning">The tune metric, which selects the detection.</param>
    /// <param name="allowScreenContentTools">Receives whether palette tools are enabled.</param>
    /// <param name="allowIntraBlockCopy">Receives whether intra block copy is enabled.</param>
    /// <returns>Whether the frame is screen content for encoder decisions.</returns>
    public static abstract bool DetectScreenContent(
        Av1EncoderFrame<TSample> source,
        HeifEncodingSpeed speed,
        Av1Tuning tuning,
        out bool allowScreenContentTools,
        out bool allowIntraBlockCopy);
}
