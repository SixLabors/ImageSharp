// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

/// <summary>
/// Applies AV1 forward quantization to raster-order transform coefficients.
/// </summary>
internal static partial class Av1ForwardQuantizer
{
    /// <summary>
    /// Quantizes one lossy transform block with the fast quantization arithmetic. A quantization matrix selects the matrix arithmetic.
    /// </summary>
    /// <param name="coefficients">The raster-order forward-transform coefficients.</param>
    /// <param name="quantizedCoefficients">The raster-order entropy-coding coefficients.</param>
    /// <param name="dequantizedCoefficients">The raster-order reconstruction coefficients.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="sharpness">The encoder sharpness, 0 to 7, which sets the rounding.</param>
    /// <param name="weights">The forward quantization matrix, or an empty span for a flat matrix.</param>
    /// <param name="inverseWeights">The inverse quantization matrix, or an empty span for a flat matrix.</param>
    /// <returns>The one-based end position in coefficient scan order.</returns>
    public static ushort QuantizeLossy(
        ReadOnlySpan<int> coefficients,
        Span<int> quantizedCoefficients,
        Span<int> dequantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1BitDepth bitDepth,
        int sharpness,
        ReadOnlySpan<byte> weights,
        ReadOnlySpan<byte> inverseWeights)
    {
        // A quantization matrix uses the matrix path at every bit depth. Without a matrix, the bit depth selects the operator.
        if (!weights.IsEmpty)
        {
            return QuantizeWithMatrix(
                coefficients, quantizedCoefficients, dequantizedCoefficients, transformSize, transformType, qIndex, dcDeltaQ, acDeltaQ, bitDepth, sharpness, weights, inverseWeights, regular: false);
        }

        ReadOnlySpan<short> inverseScan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).InverseScan;
        if (bitDepth != Av1BitDepth.EightBit)
        {
            return Quantize<HighBitDepthFastQuantizationOperator>(
                coefficients,
                quantizedCoefficients,
                dequantizedCoefficients,
                transformSize,
                inverseScan,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                bitDepth,
                sharpness,
                scanOrder: false);
        }

        return Quantize<FastQuantizationOperator>(
            coefficients,
            quantizedCoefficients,
            dequantizedCoefficients,
            transformSize,
            inverseScan,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            bitDepth,
            sharpness,
            scanOrder: false);
    }

    /// <summary>
    /// Quantizes one reversible four-by-four transform without changing its reconstruction coefficients.
    /// </summary>
    /// <param name="coefficients">The raster-order reversible-transform coefficients.</param>
    /// <param name="quantizedCoefficients">The raster-order entropy-coding coefficients.</param>
    /// <param name="dequantizedCoefficients">The raster-order reconstruction coefficients.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <returns>The one-based end position in coefficient scan order.</returns>
    public static ushort QuantizeLossless(
        ReadOnlySpan<int> coefficients,
        Span<int> quantizedCoefficients,
        Span<int> dequantizedCoefficients,
        Av1BitDepth bitDepth)
        => Quantize<LosslessQuantizationOperator>(
            coefficients,
            quantizedCoefficients,
            dequantizedCoefficients,
            Av1TransformSize.Size4x4,
            Av1ScanOrderConstants.GetScanOrder(Av1TransformSize.Size4x4, Av1TransformType.DctDct).InverseScan,
            0,
            0,
            0,
            bitDepth,
            0,
            scanOrder: false);

    /// <summary>
    /// Quantizes estimation coefficients in their specified scan order.
    /// </summary>
    /// <param name="coefficients">The estimation transform coefficients.</param>
    /// <param name="quantizedCoefficients">The quantized coefficient destination.</param>
    /// <param name="dequantizedCoefficients">The reconstructed coefficient destination.</param>
    /// <param name="transformSize">The square estimation transform size.</param>
    /// <param name="scan">The scan indices for the estimation coefficient layout.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The AC quantizer adjustment.</param>
    /// <param name="bitDepth">The source sample precision.</param>
    /// <param name="sharpness">The encoder sharpness, 0 to 7, which sets the rounding.</param>
    /// <returns>The one-based end position in the supplied scan.</returns>
    public static ushort QuantizeForModeEstimation(
        ReadOnlySpan<int> coefficients,
        Span<int> quantizedCoefficients,
        Span<int> dequantizedCoefficients,
        Av1TransformSize transformSize,
        ReadOnlySpan<short> scan,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1BitDepth bitDepth,
        int sharpness)
        => Quantize<FastQuantizationOperator>(
            coefficients,
            quantizedCoefficients,
            dequantizedCoefficients,
            transformSize,
            scan,
            qIndex,
            dcDeltaQ,
            acDeltaQ,
            bitDepth,
            sharpness,
            scanOrder: true);

    /// <summary>
    /// Applies a closed generic quantization operator across the widest available hardware widths.
    /// </summary>
    /// <typeparam name="TOperator">The quantization arithmetic.</typeparam>
    /// <param name="coefficients">The raster-order transform coefficients.</param>
    /// <param name="quantizedCoefficients">The raster-order entropy-coding coefficients.</param>
    /// <param name="dequantizedCoefficients">The raster-order reconstruction coefficients.</param>
    /// <param name="transformSize">The transform-block dimensions.</param>
    /// <param name="inverseScan">The scan position of each raster-order coefficient, or the scan itself when <paramref name="scanOrder"/> is set.</param>
    /// <param name="qIndex">The segment quantizer index.</param>
    /// <param name="dcDeltaQ">The plane DC quantizer adjustment.</param>
    /// <param name="acDeltaQ">The plane AC quantizer adjustment.</param>
    /// <param name="bitDepth">The coded sample bit depth.</param>
    /// <param name="sharpness">The encoder sharpness, 0 to 7, which sets the rounding.</param>
    /// <param name="scanOrder">Whether <paramref name="inverseScan"/> holds a scan in place of an inverse scan.</param>
    /// <returns>The one-based end position in coefficient scan order.</returns>
    private static ushort Quantize<TOperator>(
        ReadOnlySpan<int> coefficients,
        Span<int> quantizedCoefficients,
        Span<int> dequantizedCoefficients,
        Av1TransformSize transformSize,
        ReadOnlySpan<short> inverseScan,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1BitDepth bitDepth,
        int sharpness,
        bool scanOrder)
        where TOperator : struct, IForwardQuantizationOperator
    {
        int coefficientCount = transformSize.GetAdjusted().GetSize2d();
        int logScale = transformSize.GetScale();
        int dcDequantizer = Av1QuantizationLookup.GetDcQuant(qIndex, dcDeltaQ, bitDepth);
        int acDequantizer = Av1QuantizationLookup.GetAcQuant(qIndex, acDeltaQ, bitDepth);
        int dcQuantizer = Av1QuantizationLookup.GetDcQuantizer(qIndex, dcDeltaQ, bitDepth);
        int acQuantizer = Av1QuantizationLookup.GetAcQuantizer(qIndex, acDeltaQ, bitDepth);

        // A nonzero sharpness sets the rounding factor to 64 - 16 * (7 - sharpness) / 7 for every nonzero quantizer index.
        // Sharpness 0 and quantizer index 0 keep the factor of 64.
        int roundingFactor = sharpness != 0 && qIndex != 0 ? 64 - (16 * (7 - sharpness) / 7) : 64;
        int dcRounding = RoundPowerOfTwo((roundingFactor * dcDequantizer) >> 7, logScale);
        int acRounding = RoundPowerOfTwo((roundingFactor * acDequantizer) >> 7, logScale);

        ref int sourceBase = ref MemoryMarshal.GetReference(coefficients);
        ref int quantizedBase = ref MemoryMarshal.GetReference(quantizedCoefficients);
        ref int dequantizedBase = ref MemoryMarshal.GetReference(dequantizedCoefficients);
        ref short inverseScanBase = ref MemoryMarshal.GetReference(inverseScan);
        nuint count = (nuint)coefficientCount;
        DebugGuard.IsTrue((coefficientCount & 15) == 0, "Every coded transform has a multiple of 16 coefficients.");

        // Every coded transform has a multiple of 16 coefficients, so the widest vector covers the whole block with no remainder.
        // Raster coefficient zero is the only DC coefficient. The first vector carries the DC constants in lane zero and the AC
        // constants in every other lane, so the DC coefficient needs no separate scalar step. Every later vector uses the AC constants alone.
        // With an inverse scan, each vector also raises a per-lane maximum of the one-based scan position of its nonzero coefficients.
        // Then the end of block comes out of the same pass with no second read of the block.
        int endOfBlock = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<int> rounding = Vector512.Create(acRounding).WithElement(0, dcRounding);
            Vector512<int> quantizer = Vector512.Create(acQuantizer).WithElement(0, dcQuantizer);
            Vector512<int> dequantizer = Vector512.Create(acDequantizer).WithElement(0, dcDequantizer);
            Vector512<int> maximum = Vector512<int>.Zero;
            for (nuint i = 0; i < count; i += (nuint)Vector512<int>.Count)
            {
                Vector512<int> quantized = TOperator.Quantize(
                    Vector512.LoadUnsafe(ref sourceBase, i), rounding, quantizer, dequantizer, logScale, out Vector512<int> dequantized);

                quantized.StoreUnsafe(ref quantizedBase, i);
                dequantized.StoreUnsafe(ref dequantizedBase, i);
                if (!scanOrder)
                {
                    maximum = Av1CoefficientMeasures.CoefficientMeasureOperator.AccumulateEndOfBlock(quantized, ref inverseScanBase, i, maximum);
                }

                // Lane zero of the first vector was the DC coefficient. The rest of the block is AC.
                rounding = Vector512.Create(acRounding);
                quantizer = Vector512.Create(acQuantizer);
                dequantizer = Vector512.Create(acDequantizer);
            }

            Vector256<int> halves = Vector256.Max(maximum.GetLower(), maximum.GetUpper());
            endOfBlock = GetLaneMaximum(Vector128.Max(halves.GetLower(), halves.GetUpper()));
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            Vector256<int> rounding = Vector256.Create(acRounding).WithElement(0, dcRounding);
            Vector256<int> quantizer = Vector256.Create(acQuantizer).WithElement(0, dcQuantizer);
            Vector256<int> dequantizer = Vector256.Create(acDequantizer).WithElement(0, dcDequantizer);
            Vector256<int> maximum = Vector256<int>.Zero;
            for (nuint i = 0; i < count; i += (nuint)Vector256<int>.Count)
            {
                Vector256<int> quantized = TOperator.Quantize(
                    Vector256.LoadUnsafe(ref sourceBase, i), rounding, quantizer, dequantizer, logScale, out Vector256<int> dequantized);

                quantized.StoreUnsafe(ref quantizedBase, i);
                dequantized.StoreUnsafe(ref dequantizedBase, i);
                if (!scanOrder)
                {
                    maximum = Av1CoefficientMeasures.CoefficientMeasureOperator.AccumulateEndOfBlock(quantized, ref inverseScanBase, i, maximum);
                }

                // Lane zero of the first vector was the DC coefficient. The rest of the block is AC.
                rounding = Vector256.Create(acRounding);
                quantizer = Vector256.Create(acQuantizer);
                dequantizer = Vector256.Create(acDequantizer);
            }

            endOfBlock = GetLaneMaximum(Vector128.Max(maximum.GetLower(), maximum.GetUpper()));
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> rounding = Vector128.Create(acRounding).WithElement(0, dcRounding);
            Vector128<int> quantizer = Vector128.Create(acQuantizer).WithElement(0, dcQuantizer);
            Vector128<int> dequantizer = Vector128.Create(acDequantizer).WithElement(0, dcDequantizer);
            Vector128<int> maximum = Vector128<int>.Zero;
            for (nuint i = 0; i < count; i += (nuint)Vector128<int>.Count)
            {
                Vector128<int> quantized = TOperator.Quantize(
                    Vector128.LoadUnsafe(ref sourceBase, i), rounding, quantizer, dequantizer, logScale, out Vector128<int> dequantized);

                quantized.StoreUnsafe(ref quantizedBase, i);
                dequantized.StoreUnsafe(ref dequantizedBase, i);
                if (!scanOrder)
                {
                    maximum = Av1CoefficientMeasures.CoefficientMeasureOperator.AccumulateEndOfBlock(quantized, ref inverseScanBase, i, maximum);
                }

                // Lane zero of the first vector was the DC coefficient. The rest of the block is AC.
                rounding = Vector128.Create(acRounding);
                quantizer = Vector128.Create(acQuantizer);
                dequantizer = Vector128.Create(acDequantizer);
            }

            endOfBlock = GetLaneMaximum(maximum);
        }
        else
        {
            // Without vector hardware, the DC coefficient uses its own constants and every other coefficient the AC ones.
            Unsafe.Add(ref quantizedBase, 0) = TOperator.Quantize(
                Unsafe.Add(ref sourceBase, 0), dcRounding, dcQuantizer, dcDequantizer, logScale, out Unsafe.Add(ref dequantizedBase, 0));

            for (nuint i = 1; i < count; i++)
            {
                Unsafe.Add(ref quantizedBase, i) = TOperator.Quantize(
                    Unsafe.Add(ref sourceBase, i), acRounding, acQuantizer, acDequantizer, logScale, out Unsafe.Add(ref dequantizedBase, i));
            }

            if (!scanOrder)
            {
                return GetEndOfBlock(quantizedCoefficients[..coefficientCount], inverseScan);
            }
        }

        if (!scanOrder)
        {
            return (ushort)endOfBlock;
        }

        // Mode estimation supplies its own scan without an inverse. A reverse traversal of that scan
        // finds the final nonzero position.
        for (int scanIndex = coefficientCount - 1; scanIndex >= 0; scanIndex--)
        {
            if (Unsafe.Add(ref quantizedBase, inverseScan[scanIndex]) != 0)
            {
                return (ushort)(scanIndex + 1);
            }
        }

        return 0;
    }

    /// <summary>
    /// Finds the one-based scan position of the last nonzero coefficient.
    /// </summary>
    /// <remarks>
    /// Quantized coefficients stay in raster order for reconstruction and entropy coding. The end position is the largest inverse-scan
    /// index of a nonzero coefficient. Vector lanes find it without a scan-order gather.
    /// </remarks>
    /// <param name="quantized">The raster-order quantized coefficients.</param>
    /// <param name="inverseScan">The scan position of each raster-order coefficient.</param>
    /// <returns>The one-based end position in coefficient scan order, or zero for an empty block.</returns>
    public static ushort GetEndOfBlock(ReadOnlySpan<int> quantized, ReadOnlySpan<short> inverseScan)
        => Av1CoefficientMeasures.GetEndOfBlock(quantized, inverseScan);

    /// <summary>
    /// Gets the largest lane of the per-lane end-of-block maxima that the quantize loop kept.
    /// </summary>
    /// <param name="maximum">The per-lane maxima.</param>
    /// <returns>The one-based end position in coefficient scan order, or zero for an empty block.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetLaneMaximum(Vector128<int> maximum)
    {
        // Fold the lane pairs twice, so that lane zero holds the maximum of all four lanes.
        Vector128<int> folded = Vector128.Max(maximum, Vector128.Shuffle(maximum, Vector128.Create(2, 3, 0, 1)));
        folded = Vector128.Max(folded, Vector128.Shuffle(folded, Vector128.Create(1, 0, 3, 2)));
        return folded.ToScalar();
    }

    /// <summary>
    /// Divides a non-negative quantizer constant by 2^<paramref name="shift"/> and rounds half up.
    /// </summary>
    /// <param name="value">The non-negative constant.</param>
    /// <param name="shift">The power-of-two exponent. Zero returns the value unchanged.</param>
    /// <returns>The rounded quotient.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundPowerOfTwo(int value, int shift)
        => shift == 0 ? value : (value + (1 << (shift - 1))) >> shift;
}
