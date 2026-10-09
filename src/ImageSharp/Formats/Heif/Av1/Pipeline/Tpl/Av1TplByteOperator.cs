// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// Closes the model kernels over eight-bit samples.
/// </summary>
internal readonly struct Av1TplByteOperator : IAv1TplSampleOperator<byte>
{
    /// <inheritdoc/>
    public static byte CreateSample(int value) => (byte)value;

    /// <inheritdoc/>
    public static int GetSampleValue(byte sample) => sample;

    /// <inheritdoc/>
    public static void Subtract(
        ReadOnlySpan<byte> source,
        int sourceStride,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<short> residual,
        int width,
        int height)
        => Av1ResidualBuilder.Subtract(source, sourceStride, prediction, predictionStride, residual, width, width, height);

    /// <inheritdoc/>
    public static void PredictDc(
        bool hasLeft,
        bool hasAbove,
        Span<byte> destination,
        int stride,
        ReadOnlySpan<byte> above,
        ReadOnlySpan<byte> left,
        int width,
        int height,
        int bitDepth)
        => Av1DcIntraPredictor.Predict(hasLeft, hasAbove, destination, stride, above, left, width, height);

    /// <inheritdoc/>
    public static void PredictNonDirectional(
        Av1PredictionMode mode,
        Span<byte> destination,
        int stride,
        ReadOnlySpan<byte> above,
        ReadOnlySpan<byte> left,
        int width,
        int height)
        => Av1NonDirectionalIntraPredictorBase.GetPredictor(mode).Predict(destination, stride, above, left, width, height);

    /// <inheritdoc/>
    public static void PrepareDirectionalEdges(
        Span<byte> above,
        Span<byte> left,
        int width,
        int height,
        int angle,
        int topCount,
        int leftCount,
        bool filterType,
        int bitDepth,
        Span<byte> originalEdge,
        out bool upsampleAbove,
        out bool upsampleLeft)
        => Av1IntraEdgePreparation.Prepare(
            above,
            left,
            width,
            height,
            angle,
            topCount,
            leftCount,
            filterType,
            bitDepth,
            originalEdge,
            out upsampleAbove,
            out upsampleLeft);

    /// <inheritdoc/>
    public static void PredictDirectional(
        Span<byte> destination,
        int stride,
        Av1TransformSize transformSize,
        ReadOnlySpan<byte> above,
        ReadOnlySpan<byte> left,
        bool upsampleAbove,
        bool upsampleLeft,
        int angle,
        Span<byte> transposedBlock)
        => Av1DirectionalIntraPredictor.Predict(destination, stride, transformSize, above, left, upsampleAbove, upsampleLeft, angle, transposedBlock);

    /// <inheritdoc/>
    public static void PredictTranslational(
        ReadOnlySpan<byte> reference,
        int referenceStride,
        int referenceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        int horizontalPhase,
        int verticalPhase,
        int bitDepth,
        Span<short> intermediateRows)
        => Av1TranslationalInterPredictor.Predict(
            reference,
            referenceStride,
            referenceOrigin,
            destination,
            destinationStride,
            width,
            height,
            Av1InterpolationFilter.Regular,
            Av1InterpolationFilter.Regular,
            horizontalPhase,
            verticalPhase,
            intermediateRows);

    /// <inheritdoc/>
    public static void PredictCompoundIntermediate(
        ReadOnlySpan<byte> reference,
        int referenceStride,
        int referenceOrigin,
        Span<ushort> destination,
        int destinationStride,
        int width,
        int height,
        int horizontalPhase,
        int verticalPhase,
        int bitDepth,
        Span<short> intermediateRows)
        => Av1CompoundInterPredictor.PredictCompound(
            reference,
            referenceStride,
            referenceOrigin,
            destination,
            destinationStride,
            width,
            height,
            Av1InterpolationFilter.Regular,
            Av1InterpolationFilter.Regular,
            horizontalPhase,
            verticalPhase,
            intermediateRows);

    /// <inheritdoc/>
    public static void AverageCompound(
        Span<byte> destination,
        int destinationStride,
        ReadOnlySpan<ushort> first,
        ReadOnlySpan<ushort> second,
        int width,
        int height,
        int bitDepth)
        => Av1CompoundIntermediateAveragePredictor.AverageIntermediate(destination, destinationStride, first, width, second, width, width, height, bitDepth);

    /// <inheritdoc/>
    public static void Reconstruct(
        ReadOnlySpan<int> coefficients,
        Span<byte> destination,
        int stride,
        Av1TransformSize transformSize,
        int endOfBlock,
        Av1BitDepth bitDepth,
        Span<int> workspace)
        => Av1InverseTransformer.Reconstruct8Bit(
            coefficients,
            destination,
            stride,
            transformSize,
            Av1TransformType.DctDct,
            0,
            endOfBlock,
            false,
            workspace);
}
