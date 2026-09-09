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
    /// Quantizes one lossy transform block with libaom's fast no-matrix arithmetic.
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
        Av1BitDepth bitDepth)
        => bitDepth == Av1BitDepth.EightBit
            ? Quantize<FastQuantizationOperator>(
                coefficients,
                quantizedCoefficients,
                dequantizedCoefficients,
                transformSize,
                transformType,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                bitDepth)
            : Quantize<HighBitDepthFastQuantizationOperator>(
                coefficients,
                quantizedCoefficients,
                dequantizedCoefficients,
                transformSize,
                transformType,
                qIndex,
                dcDeltaQ,
                acDeltaQ,
                bitDepth);

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
            Av1TransformType.DctDct,
            0,
            0,
            0,
            bitDepth);

    /// <summary>
    /// Applies a closed generic quantization operator across the widest available hardware widths.
    /// </summary>
    private static ushort Quantize<TOperator>(
        ReadOnlySpan<int> coefficients,
        Span<int> quantizedCoefficients,
        Span<int> dequantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1BitDepth bitDepth)
        where TOperator : struct, IForwardQuantizationOperator
    {
        int coefficientCount = transformSize.GetAdjusted().GetSize2d();
        int logScale = transformSize.GetScale();
        int dcDequantizer = Av1QuantizationLookup.GetDcQuant(qIndex, dcDeltaQ, bitDepth);
        int acDequantizer = Av1QuantizationLookup.GetAcQuant(qIndex, acDeltaQ, bitDepth);
        int dcQuantizer = (1 << 16) / dcDequantizer;
        int acQuantizer = (1 << 16) / acDequantizer;
        int dcRounding = RoundPowerOfTwo((64 * dcDequantizer) >> 7, logScale);
        int acRounding = RoundPowerOfTwo((64 * acDequantizer) >> 7, logScale);

        ref int sourceBase = ref MemoryMarshal.GetReference(coefficients);
        ref int quantizedBase = ref MemoryMarshal.GetReference(quantizedCoefficients);
        ref int dequantizedBase = ref MemoryMarshal.GetReference(dequantizedCoefficients);

        // Raster coefficient zero is the only DC coefficient, so it is encoded once with the plane's DC constants
        // before the AC-only SIMD traversal begins.
        Unsafe.Add(ref quantizedBase, 0) = TOperator.Quantize(
            Unsafe.Add(ref sourceBase, 0),
            dcRounding,
            dcQuantizer,
            dcDequantizer,
            logScale,
            out Unsafe.Add(ref dequantizedBase, 0));

        int i = 1;

        // Raster traversal keeps loads and stores contiguous. Each narrower tier resumes at the shared offset left by
        // the previous tier, retaining vector execution for the widest possible remainder without overlapping lanes.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = coefficients[i..coefficientCount].Vector512Count<int>();

            if (vectorCount > 0)
            {
                // Width-specific constants are created only when at least one complete vector remains.
                Vector512<int> rounding = Vector512.Create(acRounding);
                Vector512<int> quantizer = Vector512.Create(acQuantizer);
                Vector512<int> dequantizer = Vector512.Create(acDequantizer);

                for (; vectorCount > 0; vectorCount--, i += Vector512<int>.Count)
                {
                    Vector512<int> source = Unsafe.As<int, Vector512<int>>(ref Unsafe.Add(ref sourceBase, i));
                    Vector512<int> quantized = TOperator.Quantize(
                        source,
                        rounding,
                        quantizer,
                        dequantizer,
                        logScale,
                        out Vector512<int> dequantized);

                    Unsafe.As<int, Vector512<int>>(ref Unsafe.Add(ref quantizedBase, i)) = quantized;
                    Unsafe.As<int, Vector512<int>>(ref Unsafe.Add(ref dequantizedBase, i)) = dequantized;
                }
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = coefficients[i..coefficientCount].Vector256Count<int>();

            if (vectorCount > 0)
            {
                // The shared offset exposes only the remainder left by wider lanes, so no coefficient is revisited.
                Vector256<int> rounding = Vector256.Create(acRounding);
                Vector256<int> quantizer = Vector256.Create(acQuantizer);
                Vector256<int> dequantizer = Vector256.Create(acDequantizer);

                for (; vectorCount > 0; vectorCount--, i += Vector256<int>.Count)
                {
                    Vector256<int> source = Unsafe.As<int, Vector256<int>>(ref Unsafe.Add(ref sourceBase, i));
                    Vector256<int> quantized = TOperator.Quantize(
                        source,
                        rounding,
                        quantizer,
                        dequantizer,
                        logScale,
                        out Vector256<int> dequantized);

                    Unsafe.As<int, Vector256<int>>(ref Unsafe.Add(ref quantizedBase, i)) = quantized;
                    Unsafe.As<int, Vector256<int>>(ref Unsafe.Add(ref dequantizedBase, i)) = dequantized;
                }
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = coefficients[i..coefficientCount].Vector128Count<int>();

            if (vectorCount > 0)
            {
                // The final SIMD tier consumes complete four-lane groups and leaves fewer than four coefficients.
                Vector128<int> rounding = Vector128.Create(acRounding);
                Vector128<int> quantizer = Vector128.Create(acQuantizer);
                Vector128<int> dequantizer = Vector128.Create(acDequantizer);

                for (; vectorCount > 0; vectorCount--, i += Vector128<int>.Count)
                {
                    Vector128<int> source = Unsafe.As<int, Vector128<int>>(ref Unsafe.Add(ref sourceBase, i));
                    Vector128<int> quantized = TOperator.Quantize(
                        source,
                        rounding,
                        quantizer,
                        dequantizer,
                        logScale,
                        out Vector128<int> dequantized);

                    Unsafe.As<int, Vector128<int>>(ref Unsafe.Add(ref quantizedBase, i)) = quantized;
                    Unsafe.As<int, Vector128<int>>(ref Unsafe.Add(ref dequantizedBase, i)) = dequantized;
                }
            }
        }

        // On SIMD-capable systems this loop receives only the final zero-to-three AC coefficients.
        for (; i < coefficientCount; i++)
        {
            Unsafe.Add(ref quantizedBase, i) = TOperator.Quantize(
                Unsafe.Add(ref sourceBase, i),
                acRounding,
                acQuantizer,
                acDequantizer,
                logScale,
                out Unsafe.Add(ref dequantizedBase, i));
        }

        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;

        // Quantized coefficients remain in raster order for reconstruction and entropy coding. A reverse scan finds
        // the final nonzero position without another buffer, and normally exits on its first iteration at high quality.
        for (int scanIndex = coefficientCount - 1; scanIndex >= 0; scanIndex--)
        {
            if (Unsafe.Add(ref quantizedBase, scan[scanIndex]) != 0)
            {
                return (ushort)(scanIndex + 1);
            }
        }

        return 0;
    }

    /// <summary>
    /// Applies libaom's positive round-power-of-two operation to one quantizer constant.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundPowerOfTwo(int value, int shift)
        => shift == 0 ? value : (value + (1 << (shift - 1))) >> shift;
}
