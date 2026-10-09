// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// Binds the model to the sample-type overloads of the shared prediction, residual and reconstruction kernels. Every
/// member forwards to an existing SIMD operator traversal. The interface only closes the sample type.
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
    /// <returns>The value.</returns>
    public static abstract int GetSampleValue(TSample sample);

    /// <summary>
    /// Subtracts a prediction from its source into a packed residual.
    /// </summary>
    /// <param name="source">The source samples at the block origin.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="prediction">The prediction samples at the block origin.</param>
    /// <param name="predictionStride">The prediction row stride.</param>
    /// <param name="residual">The residual destination, packed at the block width.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    public static abstract void Subtract(
        ReadOnlySpan<TSample> source,
        int sourceStride,
        ReadOnlySpan<TSample> prediction,
        int predictionStride,
        Span<short> residual,
        int width,
        int height);

    /// <summary>
    /// Predicts a DC block from prepared edges.
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
    /// <param name="scratch">The edge filter workspace.</param>
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
        Span<TSample> scratch,
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
    /// <param name="scratch">The predictor workspace of at least one block.</param>
    public static abstract void PredictDirectional(
        Span<TSample> destination,
        int stride,
        Av1TransformSize transformSize,
        ReadOnlySpan<TSample> above,
        ReadOnlySpan<TSample> left,
        bool upsampleAbove,
        bool upsampleLeft,
        int angle,
        Span<TSample> scratch);

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
    /// <param name="scratch">The convolution workspace.</param>
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
        Span<short> scratch);

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
    /// <param name="scratch">The convolution workspace.</param>
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
        Span<short> scratch);

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
}
