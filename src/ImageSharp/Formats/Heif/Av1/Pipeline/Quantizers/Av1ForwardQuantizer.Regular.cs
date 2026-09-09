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
        int sharpness)
        => bitDepth == Av1BitDepth.EightBit
            ? QuantizeRegular<RegularQuantizationOperator>(
                coefficients, quantizedCoefficients, dequantizedCoefficients, transformSize, transformType, qIndex, dcDeltaQ, acDeltaQ, bitDepth, sharpness)
            : QuantizeRegular<HighBitDepthRegularQuantizationOperator>(
                coefficients, quantizedCoefficients, dequantizedCoefficients, transformSize, transformType, qIndex, dcDeltaQ, acDeltaQ, bitDepth, sharpness);

    /// <summary>
    /// Traverses regular quantization with one DC coefficient followed by contiguous AC vectors and a scalar tail.
    /// </summary>
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
        Av1InverseTransformMath.InvertQuantization(out int dcQuantizer, out int dcShift, dcDequantizer);
        Av1InverseTransformMath.InvertQuantization(out int acQuantizer, out int acShift, acDequantizer);

        // Zero-bin constants round twice: once from Q7 and once for the transform scale. Rounding
        // constants first truncate from Q7, then round for that same scale. Combining either pair of
        // shifts would change coefficients at the quantization boundary.
        int dcZeroBin = RoundPowerOfTwo(RoundPowerOfTwo(zeroBinFactor * dcDequantizer, 7), logScale);
        int acZeroBin = RoundPowerOfTwo(RoundPowerOfTwo(zeroBinFactor * acDequantizer, 7), logScale);
        int dcRounding = RoundPowerOfTwo((roundingFactor * dcDequantizer) >> 7, logScale);
        int acRounding = RoundPowerOfTwo((roundingFactor * acDequantizer) >> 7, logScale);
        ref int sourceBase = ref MemoryMarshal.GetReference(coefficients);
        ref int quantizedBase = ref MemoryMarshal.GetReference(quantizedCoefficients);
        ref int dequantizedBase = ref MemoryMarshal.GetReference(dequantizedCoefficients);

        // DC has different constants. Processing it once leaves a uniform AC traversal with no lane masks
        // for DC and no scratch coefficient copy; every output position is overwritten on each candidate.
        quantizedBase = TOperator.Quantize(
            sourceBase, dcZeroBin, dcRounding, dcQuantizer, dcShift, dcDequantizer, logScale, out dequantizedBase);

        int index = 1;

        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<int> zeroBin = Vector512.Create(acZeroBin);
            Vector512<int> rounding = Vector512.Create(acRounding);
            Vector512<int> quantizer = Vector512.Create(acQuantizer);
            Vector512<int> shift = Vector512.Create(acShift);
            Vector512<int> dequantizer = Vector512.Create(acDequantizer);

            // Each lane owns one contiguous coefficient. Narrower tiers resume at the first unread
            // coefficient so unaligned starts and vector tails need neither padding nor overlapping stores.
            for (; index <= count - Vector512<int>.Count; index += Vector512<int>.Count)
            {
                Vector512<int> values = Vector512.LoadUnsafe(ref sourceBase, (nuint)index);
                Vector512<int> quantized = TOperator.Quantize(
                    values, zeroBin, rounding, quantizer, shift, dequantizer, logScale, out Vector512<int> dequantized);

                quantized.StoreUnsafe(ref quantizedBase, (nuint)index);
                dequantized.StoreUnsafe(ref dequantizedBase, (nuint)index);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<int> zeroBin = Vector256.Create(acZeroBin);
            Vector256<int> rounding = Vector256.Create(acRounding);
            Vector256<int> quantizer = Vector256.Create(acQuantizer);
            Vector256<int> shift = Vector256.Create(acShift);
            Vector256<int> dequantizer = Vector256.Create(acDequantizer);

            // Each lane owns one contiguous coefficient. Narrower tiers resume at the first unread
            // coefficient so unaligned starts and vector tails need neither padding nor overlapping stores.
            for (; index <= count - Vector256<int>.Count; index += Vector256<int>.Count)
            {
                Vector256<int> values = Vector256.LoadUnsafe(ref sourceBase, (nuint)index);
                Vector256<int> quantized = TOperator.Quantize(
                    values, zeroBin, rounding, quantizer, shift, dequantizer, logScale, out Vector256<int> dequantized);

                quantized.StoreUnsafe(ref quantizedBase, (nuint)index);
                dequantized.StoreUnsafe(ref dequantizedBase, (nuint)index);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> zeroBin = Vector128.Create(acZeroBin);
            Vector128<int> rounding = Vector128.Create(acRounding);
            Vector128<int> quantizer = Vector128.Create(acQuantizer);
            Vector128<int> shift = Vector128.Create(acShift);
            Vector128<int> dequantizer = Vector128.Create(acDequantizer);

            // Each lane owns one contiguous coefficient. Narrower tiers resume at the first unread
            // coefficient so unaligned starts and vector tails need neither padding nor overlapping stores.
            for (; index <= count - Vector128<int>.Count; index += Vector128<int>.Count)
            {
                Vector128<int> values = Vector128.LoadUnsafe(ref sourceBase, (nuint)index);
                Vector128<int> quantized = TOperator.Quantize(
                    values, zeroBin, rounding, quantizer, shift, dequantizer, logScale, out Vector128<int> dequantized);

                quantized.StoreUnsafe(ref quantizedBase, (nuint)index);
                dequantized.StoreUnsafe(ref dequantizedBase, (nuint)index);
            }
        }

        for (; index < count; index++)
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

        // Coding and reconstruction retain raster order. Only the end position is reduced in scan order.
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;
        for (int scanIndex = count - 1; scanIndex >= 0; scanIndex--)
        {
            if (Unsafe.Add(ref quantizedBase, scan[scanIndex]) != 0)
            {
                return (ushort)(scanIndex + 1);
            }
        }

        return 0;
    }
}
