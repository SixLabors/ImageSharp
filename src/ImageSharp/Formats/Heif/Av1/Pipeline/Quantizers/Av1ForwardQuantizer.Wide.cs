// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;

/// <content>
/// Sixteen-lane eight-bit quantizers after libaom's <c>av1_quantize_fp_avx2</c> and <c>aom_quantize_b_avx2</c> families.
/// </content>
internal static partial class Av1ForwardQuantizer
{
    /// <summary>
    /// Carries a transform scale as a type so that each quantizer specialization folds its shifts.
    /// </summary>
    private interface IQuantizerScale
    {
        /// <summary>
        /// Gets the base-two logarithm of the transform scale.
        /// </summary>
        public static abstract int LogScale { get; }
    }

    /// <summary>
    /// Gets a value indicating whether the sixteen-lane quantizers are available on this machine.
    /// </summary>
    private static bool WideSupported => Avx2.IsSupported;

    /// <summary>
    /// Quantizes an eight-bit block with the fast arithmetic of <c>av1_quantize_fp_avx2</c>,
    /// <c>av1_quantize_fp_32x32_avx2</c> or <c>av1_quantize_fp_64x64_avx2</c>.
    /// </summary>
    /// <remarks>
    /// The coefficients pack to sixteen bits as libaom's sixteen-bit forward transforms deliver them. Sixteen
    /// coefficients form one group; a group whose magnitudes all sit below the dequantizer threshold stores zeros
    /// without multiplying, and the end of block accumulates from the inverse scan in the same pass.
    /// </remarks>
    private static ushort QuantizeFastWide<TScale>(
        ReadOnlySpan<int> coefficients,
        Span<int> quantizedCoefficients,
        Span<int> dequantizedCoefficients,
        int count,
        int dcRounding,
        int acRounding,
        int dcQuantizer,
        int acQuantizer,
        int dcDequantizer,
        int acDequantizer,
        ReadOnlySpan<short> inverseScan)
        where TScale : struct, IQuantizerScale
    {
        ref int source = ref MemoryMarshal.GetReference(coefficients);
        ref int quantizedBase = ref MemoryMarshal.GetReference(quantizedCoefficients);
        ref int dequantizedBase = ref MemoryMarshal.GetReference(dequantizedCoefficients);
        ref short inverseScanBase = ref MemoryMarshal.GetReference(inverseScan);

        // init_qp: the first group carries the DC constants in lane zero; every later group is AC only.
        Vector256<short> rounding = Vector256.Create((short)acRounding).WithElement(0, (short)dcRounding);
        Vector256<short> quantizer = Vector256.Create((short)acQuantizer).WithElement(0, (short)dcQuantizer);
        Vector256<short> dequantizer = Vector256.Create((short)acDequantizer).WithElement(0, (short)dcDequantizer);
        if (TScale.LogScale == 1)
        {
            quantizer <<= 1;
        }

        Vector256<short> threshold = (dequantizer >> (1 + TScale.LogScale)) - Vector256<short>.One;
        Vector256<short> endOfBlock = Vector256<short>.Zero;
        QuantizeFastGroup<TScale>(ref source, ref quantizedBase, ref dequantizedBase, ref inverseScanBase, 0, threshold, rounding, quantizer, dequantizer, ref endOfBlock);

        // update_qp
        rounding = Vector256.Create((short)acRounding);
        quantizer = Vector256.Create((short)(TScale.LogScale == 1 ? acQuantizer << 1 : acQuantizer));
        dequantizer = Vector256.Create((short)acDequantizer);
        threshold = (dequantizer >> (1 + TScale.LogScale)) - Vector256<short>.One;
        for (int i = 16; i < count; i += 16)
        {
            QuantizeFastGroup<TScale>(ref source, ref quantizedBase, ref dequantizedBase, ref inverseScanBase, i, threshold, rounding, quantizer, dequantizer, ref endOfBlock);
        }

        return GatherEndOfBlock(endOfBlock);
    }

    /// <summary>
    /// Quantizes one group of sixteen coefficients, as <c>quantize_fp_16</c>, <c>quantize_fp_32x32</c> and
    /// <c>quantize_fp_64x64</c> do.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void QuantizeFastGroup<TScale>(
        ref int source,
        ref int quantizedBase,
        ref int dequantizedBase,
        ref short inverseScanBase,
        int offset,
        Vector256<short> threshold,
        Vector256<short> rounding,
        Vector256<short> quantizer,
        Vector256<short> dequantizer,
        ref Vector256<short> endOfBlock)
        where TScale : struct, IQuantizerScale
    {
        Vector256<short> coefficients = LoadGroup(ref source, offset);
        Vector256<short> magnitude = Avx2.Abs(coefficients).AsInt16();
        Vector256<short> mask = Avx2.CompareGreaterThan(magnitude, threshold);
        if (Avx2.MoveMask(mask.AsByte()) == 0)
        {
            StoreZeroGroup(ref quantizedBase, offset);
            StoreZeroGroup(ref dequantizedBase, offset);
            return;
        }

        Vector256<short> quantized;
        Vector256<short> dequantized;
        Vector256<short> nonzero;
        if (TScale.LogScale == 0)
        {
            Vector256<short> rounded = Avx2.AddSaturate(magnitude, rounding);
            Vector256<short> quantizedMagnitude = Avx2.MultiplyHigh(rounded, quantizer);
            quantized = Avx2.Sign(quantizedMagnitude, coefficients);
            dequantized = Avx2.MultiplyLow(quantized, dequantizer);
            nonzero = Avx2.CompareGreaterThan(quantizedMagnitude, Vector256<short>.Zero);
        }
        else if (TScale.LogScale == 1)
        {
            // The quantizer is doubled for this scale, so it reaches 32768 when the
            // dequantizer is four. libaom therefore takes the high half unsigned
            // (quantize_fp_32x32 line 276 uses _mm256_mulhi_epu16); a signed high multiply
            // would read that quantizer as negative.
            Vector256<short> rounded = Avx2.AddSaturate(magnitude, rounding);
            Vector256<short> quantizedMagnitude = Avx2.MultiplyHigh(rounded.AsUInt16(), quantizer.AsUInt16()).AsInt16();
            quantized = Avx2.Sign(quantizedMagnitude, coefficients);
            Vector256<short> dequantizedMagnitude = Avx2.ShiftRightLogical(Avx2.MultiplyLow(quantizedMagnitude, dequantizer), 1);
            nonzero = Avx2.CompareGreaterThan(quantizedMagnitude, Vector256<short>.Zero);
            dequantized = Avx2.Sign(dequantizedMagnitude, coefficients);
        }
        else
        {
            // The products need more than sixteen bits, so the high and low halves combine after their own shifts.
            Vector256<short> rounded = Avx2.AddSaturate(magnitude, rounding) & mask;
            Vector256<short> quantizedMagnitude =
                Avx2.ShiftLeftLogical(Avx2.MultiplyHigh(rounded, quantizer), 2) |
                Avx2.ShiftRightLogical(Avx2.MultiplyLow(rounded, quantizer), 14);
            Vector256<short> dequantizedMagnitude =
                Avx2.ShiftLeftLogical(Avx2.MultiplyHigh(quantizedMagnitude, dequantizer), 14) |
                Avx2.ShiftRightLogical(Avx2.MultiplyLow(quantizedMagnitude, dequantizer), 2);
            quantized = Avx2.Sign(quantizedMagnitude, coefficients);
            dequantized = Avx2.Sign(dequantizedMagnitude, coefficients);

            // A zero threshold lets a zero coefficient through the group test; the signed product is zero there.
            nonzero = ~Avx2.CompareEqual(dequantized, Vector256<short>.Zero);
        }

        StoreGroup(quantized, ref quantizedBase, offset);
        StoreGroup(dequantized, ref dequantizedBase, offset);
        endOfBlock = MaxLaneEndOfBlock(ref inverseScanBase, offset, endOfBlock, nonzero);
    }

    /// <summary>
    /// Quantizes an eight-bit block with the regular arithmetic of <c>aom_quantize_b_avx2</c>,
    /// <c>aom_quantize_b_32x32_avx2</c> or <c>aom_quantize_b_64x64_avx2</c>.
    /// </summary>
    private static ushort QuantizeRegularWide<TScale>(
        ReadOnlySpan<int> coefficients,
        Span<int> quantizedCoefficients,
        Span<int> dequantizedCoefficients,
        int count,
        int dcZeroBin,
        int acZeroBin,
        int dcRounding,
        int acRounding,
        int dcQuantizer,
        int acQuantizer,
        int dcShift,
        int acShift,
        int dcDequantizer,
        int acDequantizer,
        ReadOnlySpan<short> inverseScan)
        where TScale : struct, IQuantizerScale
    {
        ref int source = ref MemoryMarshal.GetReference(coefficients);
        ref int quantizedBase = ref MemoryMarshal.GetReference(quantizedCoefficients);
        ref int dequantizedBase = ref MemoryMarshal.GetReference(dequantizedCoefficients);
        ref short inverseScanBase = ref MemoryMarshal.GetReference(inverseScan);

        // load_b_values_avx2: the first group carries the DC constants in lane zero.
        Vector256<short> zeroBin = Vector256.Create((short)(acZeroBin - 1)).WithElement(0, (short)(dcZeroBin - 1));
        Vector256<short> rounding = Vector256.Create((short)acRounding).WithElement(0, (short)dcRounding);
        Vector256<short> quantizer = Vector256.Create((short)acQuantizer).WithElement(0, (short)dcQuantizer);
        Vector256<short> shift = Vector256.Create((short)acShift).WithElement(0, (short)dcShift);
        Vector256<short> dequantizer = Vector256.Create((short)acDequantizer).WithElement(0, (short)dcDequantizer);
        Vector256<short> endOfBlock = Vector256<short>.Zero;
        QuantizeRegularGroup<TScale>(ref source, ref quantizedBase, ref dequantizedBase, ref inverseScanBase, 0, zeroBin, rounding, quantizer, shift, dequantizer, ref endOfBlock);

        zeroBin = Vector256.Create((short)(acZeroBin - 1));
        rounding = Vector256.Create((short)acRounding);
        quantizer = Vector256.Create((short)acQuantizer);
        shift = Vector256.Create((short)acShift);
        dequantizer = Vector256.Create((short)acDequantizer);
        for (int i = 16; i < count; i += 16)
        {
            QuantizeRegularGroup<TScale>(ref source, ref quantizedBase, ref dequantizedBase, ref inverseScanBase, i, zeroBin, rounding, quantizer, shift, dequantizer, ref endOfBlock);
        }

        return GatherEndOfBlock(endOfBlock);
    }

    /// <summary>
    /// Quantizes one group of sixteen coefficients, as <c>quantize_b_logscale0_16</c> and
    /// <c>quantize_b_logscale_16</c> do.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void QuantizeRegularGroup<TScale>(
        ref int source,
        ref int quantizedBase,
        ref int dequantizedBase,
        ref short inverseScanBase,
        int offset,
        Vector256<short> zeroBin,
        Vector256<short> rounding,
        Vector256<short> quantizer,
        Vector256<short> shift,
        Vector256<short> dequantizer,
        ref Vector256<short> endOfBlock)
        where TScale : struct, IQuantizerScale
    {
        Vector256<short> coefficients = LoadGroup(ref source, offset);
        Vector256<short> magnitude = Avx2.Abs(coefficients).AsInt16();
        Vector256<short> mask = Avx2.CompareGreaterThan(magnitude, zeroBin);
        if (Avx2.MoveMask(mask.AsByte()) == 0)
        {
            StoreZeroGroup(ref quantizedBase, offset);
            StoreZeroGroup(ref dequantizedBase, offset);
            return;
        }

        // tmp32 = ((((tmp * quant) >> 16) + tmp) * quant_shift) >> (16 - log_scale)
        Vector256<short> rounded = Avx2.AddSaturate(magnitude, rounding) & mask;
        Vector256<short> corrected = Avx2.MultiplyHigh(rounded, quantizer) + rounded;
        Vector256<short> quantizedMagnitude;
        Vector256<short> dequantized;
        if (TScale.LogScale == 0)
        {
            quantizedMagnitude = Avx2.MultiplyHigh(corrected, shift);
            dequantized = Avx2.MultiplyLow(Avx2.Sign(quantizedMagnitude, coefficients), dequantizer);
        }
        else
        {
            Vector256<short> dequantizedMagnitude;
            if (TScale.LogScale == 1)
            {
                quantizedMagnitude =
                    Avx2.ShiftLeftLogical(Avx2.MultiplyHigh(corrected, shift), 1) |
                    Avx2.ShiftRightLogical(Avx2.MultiplyLow(corrected, shift), 15);
                dequantizedMagnitude =
                    Avx2.ShiftLeftLogical(Avx2.MultiplyHigh(quantizedMagnitude, dequantizer), 15) |
                    Avx2.ShiftRightLogical(Avx2.MultiplyLow(quantizedMagnitude, dequantizer), 1);
            }
            else
            {
                quantizedMagnitude =
                    Avx2.ShiftLeftLogical(Avx2.MultiplyHigh(corrected, shift), 2) |
                    Avx2.ShiftRightLogical(Avx2.MultiplyLow(corrected, shift), 14);
                dequantizedMagnitude =
                    Avx2.ShiftLeftLogical(Avx2.MultiplyHigh(quantizedMagnitude, dequantizer), 14) |
                    Avx2.ShiftRightLogical(Avx2.MultiplyLow(quantizedMagnitude, dequantizer), 2);
            }

            dequantized = Avx2.Sign(dequantizedMagnitude, coefficients);
        }

        Vector256<short> nonzero = Avx2.CompareGreaterThan(quantizedMagnitude, Vector256<short>.Zero);
        StoreGroup(Avx2.Sign(quantizedMagnitude, coefficients), ref quantizedBase, offset);
        StoreGroup(dequantized, ref dequantizedBase, offset);
        endOfBlock = MaxLaneEndOfBlock(ref inverseScanBase, offset, endOfBlock, nonzero);
    }

    /// <summary>
    /// Packs sixteen coefficients to sixteen bits, as <c>load_coefficients_avx2</c> does. The packed order interleaves
    /// the two halves of each eight-coefficient run, which the store and the scan lookup undo the same way.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> LoadGroup(ref int source, int offset)
        => Avx2.PackSignedSaturate(Vector256.LoadUnsafe(ref source, (nuint)offset), Vector256.LoadUnsafe(ref source, (nuint)(offset + 8)));

    /// <summary>
    /// Widens sixteen packed values to thirty-two bits and stores them, as <c>store_coefficients_avx2</c> does.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreGroup(Vector256<short> values, ref int destination, int offset)
    {
        Vector256<short> sign = Avx2.ShiftRightArithmetic(values, 15);
        Avx2.UnpackLow(values, sign).AsInt32().StoreUnsafe(ref destination, (nuint)offset);
        Avx2.UnpackHigh(values, sign).AsInt32().StoreUnsafe(ref destination, (nuint)(offset + 8));
    }

    /// <summary>
    /// Stores sixteen zero coefficients.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreZeroGroup(ref int destination, int offset)
    {
        Vector256<int>.Zero.StoreUnsafe(ref destination, (nuint)offset);
        Vector256<int>.Zero.StoreUnsafe(ref destination, (nuint)(offset + 8));
    }

    /// <summary>
    /// Raises the running end of block by the scan positions of the nonzero lanes, as <c>get_max_lane_eob</c> does.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> MaxLaneEndOfBlock(ref short inverseScanBase, int offset, Vector256<short> endOfBlock, Vector256<short> nonzero)
    {
        Vector256<short> positions = Avx2.Permute4x64(Vector256.LoadUnsafe(ref inverseScanBase, (nuint)offset).AsInt64(), 0xD8).AsInt16();
        return Avx2.Max(endOfBlock, (positions - nonzero) & nonzero);
    }

    /// <summary>
    /// Reduces the lane maxima to the end of block, as <c>quant_gather_eob</c> does.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort GatherEndOfBlock(Vector256<short> endOfBlock)
    {
        Vector128<short> maximum = Sse2.Max(endOfBlock.GetLower(), endOfBlock.GetUpper());
        maximum = Sse2.Max(maximum, Sse2.Shuffle(maximum.AsInt32(), 0b01_00_11_10).AsInt16());
        maximum = Sse2.Max(maximum, Sse2.ShuffleLow(maximum, 0b01_00_11_10));
        maximum = Sse2.Max(maximum, Sse2.ShuffleLow(maximum, 0b11_10_00_01));
        return (ushort)maximum.ToScalar();
    }

    private readonly struct Scale0 : IQuantizerScale
    {
        /// <inheritdoc/>
        public static int LogScale => 0;
    }

    private readonly struct Scale1 : IQuantizerScale
    {
        /// <inheritdoc/>
        public static int LogScale => 1;
    }

    private readonly struct Scale2 : IQuantizerScale
    {
        /// <inheritdoc/>
        public static int LogScale => 2;
    }
}
