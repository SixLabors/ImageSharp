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
/// Closes the first pass and the temporal dependency model kernels over high-bit-depth samples. Valid samples stay below the
/// sign bit, so the intra and reconstruction kernels read the unsigned storage through their signed overloads.
/// </summary>
internal readonly struct Av1TplUInt16Operator : IAv1TplSampleOperator<ushort>
{
    /// <inheritdoc/>
    public static ushort CreateSample(int value) => (ushort)value;

    /// <inheritdoc/>
    public static int ToInt32(ushort sample) => sample;

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
        int stride,
        ReadOnlySpan<ushort> above,
        ReadOnlySpan<ushort> left,
        int width,
        int height,
        int bitDepth)
        => Av1DcIntraPredictor.Predict(
            hasLeft,
            hasAbove,
            MemoryMarshal.Cast<ushort, short>(destination),
            stride,
            MemoryMarshal.Cast<ushort, short>(above),
            MemoryMarshal.Cast<ushort, short>(left),
            width,
            height,
            bitDepth);

    /// <inheritdoc/>
    public static void PredictNonDirectional(
        Av1PredictionMode mode,
        Span<ushort> destination,
        int stride,
        ReadOnlySpan<ushort> above,
        ReadOnlySpan<ushort> left,
        int width,
        int height)
        => Av1NonDirectionalIntraPredictorBase.GetPredictor(mode).Predict(
            MemoryMarshal.Cast<ushort, short>(destination),
            stride,
            MemoryMarshal.Cast<ushort, short>(above),
            MemoryMarshal.Cast<ushort, short>(left),
            width,
            height);

    /// <inheritdoc/>
    public static void PrepareDirectionalEdges(
        Span<ushort> above,
        Span<ushort> left,
        int width,
        int height,
        int angle,
        int topCount,
        int leftCount,
        bool filterType,
        int bitDepth,
        Span<ushort> originalEdge,
        out bool upsampleAbove,
        out bool upsampleLeft)
        => Av1IntraEdgePreparation.Prepare(
            MemoryMarshal.Cast<ushort, short>(above),
            MemoryMarshal.Cast<ushort, short>(left),
            width,
            height,
            angle,
            topCount,
            leftCount,
            filterType,
            bitDepth,
            MemoryMarshal.Cast<ushort, short>(originalEdge),
            out upsampleAbove,
            out upsampleLeft);

    /// <inheritdoc/>
    public static void PredictDirectional(
        Span<ushort> destination,
        int stride,
        Av1TransformSize transformSize,
        ReadOnlySpan<ushort> above,
        ReadOnlySpan<ushort> left,
        bool upsampleAbove,
        bool upsampleLeft,
        int angle,
        Span<ushort> transposedBlock)
        => Av1DirectionalIntraPredictor.Predict(
            MemoryMarshal.Cast<ushort, short>(destination),
            stride,
            transformSize,
            MemoryMarshal.Cast<ushort, short>(above),
            MemoryMarshal.Cast<ushort, short>(left),
            upsampleAbove,
            upsampleLeft,
            angle,
            MemoryMarshal.Cast<ushort, short>(transposedBlock));

    /// <inheritdoc/>
    public static void PredictTranslational(
        ReadOnlySpan<ushort> reference,
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
            bitDepth,
            intermediateRows);

    /// <inheritdoc/>
    public static void PredictCompoundIntermediate(
        ReadOnlySpan<ushort> reference,
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
            bitDepth,
            intermediateRows);

    /// <inheritdoc/>
    public static void AverageCompound(
        Span<ushort> destination,
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
        Span<ushort> destination,
        int stride,
        Av1TransformSize transformSize,
        int endOfBlock,
        Av1BitDepth bitDepth,
        Span<int> workspace)
        => Av1InverseTransformer.ReconstructHighBitDepth(
            coefficients,
            MemoryMarshal.Cast<ushort, short>(destination),
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
        HeifEncodingSpeed speed,
        Av1Tuning tuning,
        out bool allowScreenContentTools,
        out bool allowIntraBlockCopy)
        => Av1ScreenContentDetector.SetScreenContentOptions(source, false, speed, tuning, out allowScreenContentTools, out allowIntraBlockCopy);
}
