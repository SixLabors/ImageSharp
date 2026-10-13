// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// Closes the first pass and the temporal dependency model kernels over eight-bit samples.
/// </summary>
internal readonly struct Av1TplByteOperator : IAv1TplSampleOperator<byte>
{
    /// <inheritdoc/>
    public static byte CreateSample(int value) => (byte)value;

    /// <inheritdoc/>
    public static int ToInt32(byte sample) => sample;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> LoadWidened(ReadOnlySpan<byte> source, int index, Vector128<int> lanes)
        => Vector128.WidenLower(Vector128.WidenLower(Vector128.CreateScalar(MemoryMarshal.Read<uint>(source.Slice(index, 4))).AsByte())).AsInt32();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> LoadWidened(ReadOnlySpan<byte> source, int index, Vector256<int> lanes)
    {
        // The first widening makes eight words from the eight bytes, and the second makes eight thirty-two-bit lanes.
        Vector128<ushort> words = Vector128.WidenLower(Vector128.CreateScalar(MemoryMarshal.Read<ulong>(source.Slice(index, 8))).AsByte());
        return Vector256.WidenLower(words.ToVector256Unsafe()).AsInt32();
    }

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
            (int)Av1Plane.Y,
            endOfBlock,
            false,
            workspace);

    /// <inheritdoc/>
    public static bool DetectScreenContent(
        Av1EncoderFrame<byte> source,
        HeifEncodingSpeed speed,
        Av1Tuning tuning,
        out bool allowScreenContentTools,
        out bool allowIntraBlockCopy)
        => Av1ScreenContentDetector.SetScreenContentOptions(source, false, speed, tuning, out allowScreenContentTools, out allowIntraBlockCopy);
}
