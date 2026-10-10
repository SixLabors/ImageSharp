// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

internal static partial class Av1ForwardQuantizer
{
    /// <summary>
    /// Quantizes a transform with the regular zero-bin and reciprocal-correction arithmetic.
    /// </summary>
    /// <param name="coefficients">The raster-order transformed coefficients.</param>
    /// <param name="quantizedCoefficients">The raster-order coding coefficients.</param>
    /// <param name="dequantizedCoefficients">The raster-order reconstruction coefficients.</param>
    /// <param name="transformSize">The transform dimensions.</param>
    /// <param name="transformType">The transform type selecting coefficient scan order.</param>
    /// <param name="qIndex">The base quantizer index.</param>
    /// <param name="dcDeltaQ">The DC index adjustment.</param>
    /// <param name="acDeltaQ">The AC index adjustment.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="sharpness">The encoder sharpness setting from zero through seven.</param>
    /// <param name="weights">The forward quantization matrix, or an empty span for a flat matrix.</param>
    /// <param name="inverseWeights">The inverse quantization matrix, or an empty span for a flat matrix.</param>
    /// <returns>The one-based final nonzero scan position.</returns>
    public static ushort QuantizeRegular(
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
                coefficients, quantizedCoefficients, dequantizedCoefficients, transformSize, transformType, qIndex, dcDeltaQ, acDeltaQ, bitDepth, sharpness, weights, inverseWeights, regular: true);
        }

        if (bitDepth != Av1BitDepth.EightBit)
        {
            return QuantizeRegular<HighBitDepthRegularQuantizationOperator>(
                coefficients, quantizedCoefficients, dequantizedCoefficients, transformSize, transformType, qIndex, dcDeltaQ, acDeltaQ, bitDepth, sharpness);
        }

        return QuantizeRegular<RegularQuantizationOperator>(
            coefficients, quantizedCoefficients, dequantizedCoefficients, transformSize, transformType, qIndex, dcDeltaQ, acDeltaQ, bitDepth, sharpness);
    }

    /// <summary>
    /// Traverses regular quantization in full vectors, with the DC constants in lane zero of the first vector.
    /// </summary>
    /// <typeparam name="TOperator">The precision-specific quantization arithmetic.</typeparam>
    /// <param name="coefficients">The raster-order transformed coefficients.</param>
    /// <param name="quantizedCoefficients">The raster-order coding coefficients.</param>
    /// <param name="dequantizedCoefficients">The raster-order reconstruction coefficients.</param>
    /// <param name="transformSize">The transform dimensions.</param>
    /// <param name="transformType">The transform type selecting coefficient scan order.</param>
    /// <param name="qIndex">The base quantizer index.</param>
    /// <param name="dcDeltaQ">The DC index adjustment.</param>
    /// <param name="acDeltaQ">The AC index adjustment.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="sharpness">The encoder sharpness setting from zero through seven.</param>
    /// <returns>The one-based final nonzero scan position.</returns>
    private static ushort QuantizeRegular<TOperator>(
        ReadOnlySpan<int> coefficients,
        Span<int> quantizedCoefficients,
        Span<int> dequantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1BitDepth bitDepth,
        int sharpness)
        where TOperator : struct, IRegularQuantizationOperator
    {
        int count = transformSize.GetAdjusted().GetSize2d();
        int logScale = transformSize.GetScale();
        int zeroBinFactor = Av1InverseTransformMath.GetQzbinFactor(qIndex, bitDepth);
        int roundingFactor = qIndex == 0 ? 64 : sharpness == 0 ? 48 : 64 - (16 * (7 - sharpness) / 7);
        int dcDequantizer = Av1QuantizationLookup.GetDcQuant(qIndex, dcDeltaQ, bitDepth);
        int acDequantizer = Av1QuantizationLookup.GetAcQuant(qIndex, acDeltaQ, bitDepth);
        int dcQuantizer = Av1QuantizationLookup.GetDcRegularQuantizer(qIndex, dcDeltaQ, bitDepth, out int dcShift);
        int acQuantizer = Av1QuantizationLookup.GetAcRegularQuantizer(qIndex, acDeltaQ, bitDepth, out int acShift);

        // Zero-bin constants round twice: once from Q7 and once for the transform scale. Rounding constants first truncate from Q7,
        // then round for that same scale. A single combined shift for either pair changes coefficients at the quantization boundary.
        int dcZeroBin = RoundPowerOfTwo(RoundPowerOfTwo(zeroBinFactor * dcDequantizer, 7), logScale);
        int acZeroBin = RoundPowerOfTwo(RoundPowerOfTwo(zeroBinFactor * acDequantizer, 7), logScale);
        int dcRounding = RoundPowerOfTwo((roundingFactor * dcDequantizer) >> 7, logScale);
        int acRounding = RoundPowerOfTwo((roundingFactor * acDequantizer) >> 7, logScale);
        ref int sourceBase = ref MemoryMarshal.GetReference(coefficients);
        ref int quantizedBase = ref MemoryMarshal.GetReference(quantizedCoefficients);
        ref int dequantizedBase = ref MemoryMarshal.GetReference(dequantizedCoefficients);

        // Coding and reconstruction keep raster order. Only the end position uses scan order. In the same pass, each vector raises
        // a per-lane maximum of the one-based scan position of its nonzero coefficients.
        ref short inverseScanBase = ref MemoryMarshal.GetReference(Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).InverseScan);

        // Every coded transform has a multiple of 16 coefficients, so the widest vector covers the whole block with
        // no remainder. Raster coefficient zero is the only DC coefficient. The first vector carries the DC
        // constants in lane zero and the AC constants in every other lane, so the DC coefficient needs no separate
        // scalar step. Every later vector uses the AC constants alone.
        DebugGuard.IsTrue((count & 15) == 0, "Every coded transform has a multiple of 16 coefficients.");
        nuint length = (nuint)count;
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<int> zeroBin = Vector512.Create(acZeroBin).WithElement(0, dcZeroBin);
            Vector512<int> rounding = Vector512.Create(acRounding).WithElement(0, dcRounding);
            Vector512<int> quantizer = Vector512.Create(acQuantizer).WithElement(0, dcQuantizer);
            Vector512<int> shift = Vector512.Create(acShift).WithElement(0, dcShift);
            Vector512<int> dequantizer = Vector512.Create(acDequantizer).WithElement(0, dcDequantizer);
            Vector512<int> maximum = Vector512<int>.Zero;
            for (nuint index = 0; index < length; index += (nuint)Vector512<int>.Count)
            {
                Vector512<int> quantized = TOperator.Quantize(
                    Vector512.LoadUnsafe(ref sourceBase, index), zeroBin, rounding, quantizer, shift, dequantizer, logScale, out Vector512<int> dequantized);

                quantized.StoreUnsafe(ref quantizedBase, index);
                dequantized.StoreUnsafe(ref dequantizedBase, index);
                maximum = Av1CoefficientMeasures.CoefficientMeasureOperator.AccumulateEndOfBlock(quantized, ref inverseScanBase, index, maximum);

                // Lane zero of the first vector was the DC coefficient. The rest of the block is AC.
                zeroBin = Vector512.Create(acZeroBin);
                rounding = Vector512.Create(acRounding);
                quantizer = Vector512.Create(acQuantizer);
                shift = Vector512.Create(acShift);
                dequantizer = Vector512.Create(acDequantizer);
            }

            Vector256<int> halves = Vector256.Max(maximum.GetLower(), maximum.GetUpper());
            return (ushort)GetLaneMaximum(Vector128.Max(halves.GetLower(), halves.GetUpper()));
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            Vector256<int> zeroBin = Vector256.Create(acZeroBin).WithElement(0, dcZeroBin);
            Vector256<int> rounding = Vector256.Create(acRounding).WithElement(0, dcRounding);
            Vector256<int> quantizer = Vector256.Create(acQuantizer).WithElement(0, dcQuantizer);
            Vector256<int> shift = Vector256.Create(acShift).WithElement(0, dcShift);
            Vector256<int> dequantizer = Vector256.Create(acDequantizer).WithElement(0, dcDequantizer);
            Vector256<int> maximum = Vector256<int>.Zero;
            for (nuint index = 0; index < length; index += (nuint)Vector256<int>.Count)
            {
                Vector256<int> quantized = TOperator.Quantize(
                    Vector256.LoadUnsafe(ref sourceBase, index), zeroBin, rounding, quantizer, shift, dequantizer, logScale, out Vector256<int> dequantized);

                quantized.StoreUnsafe(ref quantizedBase, index);
                dequantized.StoreUnsafe(ref dequantizedBase, index);
                maximum = Av1CoefficientMeasures.CoefficientMeasureOperator.AccumulateEndOfBlock(quantized, ref inverseScanBase, index, maximum);

                // Lane zero of the first vector was the DC coefficient. The rest of the block is AC.
                zeroBin = Vector256.Create(acZeroBin);
                rounding = Vector256.Create(acRounding);
                quantizer = Vector256.Create(acQuantizer);
                shift = Vector256.Create(acShift);
                dequantizer = Vector256.Create(acDequantizer);
            }

            return (ushort)GetLaneMaximum(Vector128.Max(maximum.GetLower(), maximum.GetUpper()));
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> zeroBin = Vector128.Create(acZeroBin).WithElement(0, dcZeroBin);
            Vector128<int> rounding = Vector128.Create(acRounding).WithElement(0, dcRounding);
            Vector128<int> quantizer = Vector128.Create(acQuantizer).WithElement(0, dcQuantizer);
            Vector128<int> shift = Vector128.Create(acShift).WithElement(0, dcShift);
            Vector128<int> dequantizer = Vector128.Create(acDequantizer).WithElement(0, dcDequantizer);
            Vector128<int> maximum = Vector128<int>.Zero;
            for (nuint index = 0; index < length; index += (nuint)Vector128<int>.Count)
            {
                Vector128<int> quantized = TOperator.Quantize(
                    Vector128.LoadUnsafe(ref sourceBase, index), zeroBin, rounding, quantizer, shift, dequantizer, logScale, out Vector128<int> dequantized);

                quantized.StoreUnsafe(ref quantizedBase, index);
                dequantized.StoreUnsafe(ref dequantizedBase, index);
                maximum = Av1CoefficientMeasures.CoefficientMeasureOperator.AccumulateEndOfBlock(quantized, ref inverseScanBase, index, maximum);

                // Lane zero of the first vector was the DC coefficient. The rest of the block is AC.
                zeroBin = Vector128.Create(acZeroBin);
                rounding = Vector128.Create(acRounding);
                quantizer = Vector128.Create(acQuantizer);
                shift = Vector128.Create(acShift);
                dequantizer = Vector128.Create(acDequantizer);
            }

            return (ushort)GetLaneMaximum(maximum);
        }
        else
        {
            // Without vector hardware, the DC coefficient uses its own constants and every other coefficient the AC ones.
            quantizedBase = TOperator.Quantize(
                sourceBase, dcZeroBin, dcRounding, dcQuantizer, dcShift, dcDequantizer, logScale, out dequantizedBase);

            for (nuint index = 1; index < length; index++)
            {
                Unsafe.Add(ref quantizedBase, index) = TOperator.Quantize(
                    Unsafe.Add(ref sourceBase, index),
                    acZeroBin,
                    acRounding,
                    acQuantizer,
                    acShift,
                    acDequantizer,
                    logScale,
                    out Unsafe.Add(ref dequantizedBase, index));
            }
        }

        return GetEndOfBlock(
            quantizedCoefficients[..count],
            Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).InverseScan);
    }
}
