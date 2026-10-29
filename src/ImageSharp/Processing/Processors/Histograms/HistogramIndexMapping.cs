// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Processing.Processors.Histograms;

/// <summary>
/// Maps scalar measurements to stable histogram bins without measuring the input range.
/// </summary>
/// <remarks>
/// <para>
/// SDR indexing rounds measurement * (bin count - 1) to the nearest integer, with ties rounded to even.
/// Inputs must be finite and within [0,1]. The pixel conversion API supplies finite components;
/// the caller's weights must preserve the measurement range.
/// </para>
/// <para>
/// Extended indexing reads the float's exponent and leading fraction bits. The exponent selects a doubling
/// interval, and the retained fraction bits select equal-width bins within that interval. No logarithm is needed.
/// Seven fraction bits give 128 bins per interval, so 1 and 1.5 are 64 bins apart, as are 2 and 3.
/// Negative magnitudes mirror positive magnitudes around a shared zero bin, preserving numeric order.
/// </para>
/// <para>
/// Finite float magnitudes span 277 doubling intervals, from 2^-149 to the interval below 2^128.
/// Seven fraction bits therefore require (2 * 277 * 128) + 1 = 70,913 logical bins.
/// Subnormal values are multiplied by an exact power of two before extracting their bits; the exponent
/// adjustment restores their original interval. Some bins remain unused where floats have fewer fraction bits.
/// </para>
/// </remarks>
internal readonly struct HistogramIndexMapping
{
    // Binary32 has 23 stored fraction bits. Nonzero finite magnitudes occupy 277 doubling intervals,
    // from 2^-149 through the interval below 2^128. Signed magnitudes surround one shared zero bin.
    private const int FractionWidth = 23;
    private const int MinimumExponent = -149;
    private const int ExponentIntervals = 277;
    private const uint FractionMask = 0x007FFFFF;
    private const uint MinimumNormalBits = 0x00800000;
    private const float SubnormalScale = 16777216F;
    private const int SubnormalExponentAdjustment = 24;

    private readonly int fractionBits;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistogramIndexMapping"/> struct.
    /// </summary>
    /// <param name="levels">The bin count for bounded input.</param>
    /// <param name="fractionBits">The retained fraction bits, or -1 for the existing bounded mapping.</param>
    public HistogramIndexMapping(int levels, int fractionBits)
    {
        this.fractionBits = fractionBits;

        // Extended input has one bin range for each sign, plus one shared zero bin.
        this.BinCount = fractionBits < 0 ? levels : (2 * ExponentIntervals * (1 << fractionBits)) + 1;
    }

    /// <summary>
    /// Gets the complete logical bin count. Storage may omit unused pages.
    /// </summary>
    public int BinCount { get; }

    /// <summary>
    /// Gets a value indicating whether the mapping retains signed extended measurements.
    /// </summary>
    public bool IsExtended => this.fractionBits >= 0;

    /// <summary>
    /// Maps measurements into caller-owned index storage. Equal-size input and output may start at the same address.
    /// </summary>
    /// <param name="source">Measurements computed by the caller.</param>
    /// <param name="destination">The mapped indices.</param>
    public void Invoke(ReadOnlySpan<float> source, Span<int> destination)
    {
        ref float first = ref MemoryMarshal.GetReference(source);
        ref int target = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint blocks = (source.Length - i).Vector512Count<float>();
            for (nuint block = 0; block < blocks; block++, i += Vector512<float>.Count)
            {
                // Load the complete input register before writing its same-size integer result.
                this.Invoke(Vector512.LoadUnsafe(ref first, (uint)i)).StoreUnsafe(ref target, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint blocks = (source.Length - i).Vector256Count<float>();
            for (nuint block = 0; block < blocks; block++, i += Vector256<float>.Count)
            {
                // Load the complete input register before writing its same-size integer result.
                this.Invoke(Vector256.LoadUnsafe(ref first, (uint)i)).StoreUnsafe(ref target, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint blocks = (source.Length - i).Vector128Count<float>();
            for (nuint block = 0; block < blocks; block++, i += Vector128<float>.Count)
            {
                // Load the complete input register before writing its same-size integer result.
                this.Invoke(Vector128.LoadUnsafe(ref first, (uint)i)).StoreUnsafe(ref target, (uint)i);
            }
        }

        for (; i < source.Length; i++)
        {
            destination[i] = this.Invoke(source[i]);
        }
    }

    /// <summary>
    /// Maps one finite measurement. SDR measurements must be within [0,1].
    /// </summary>
    /// <param name="value">The measurement, independent of pixel layout.</param>
    /// <returns>The ordered bin index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Invoke(float value)
    {
        if (this.fractionBits < 0)
        {
            // The pixel API supplies finite SDR components in [0,1]. The RGB weights retain that range.
            // Scalar and SIMD paths use equal bin intervals and round ties to the nearest even integer.
            return (int)MathF.Round(value * (this.BinCount - 1));
        }

        // Place zero at the center. Positive magnitudes use increasing indices to its right;
        // negative magnitudes use decreasing indices to its left. Both signed zeros share this bin.
        int zero = this.BinCount / 2;
        float magnitude = MathF.Abs(value);
        if (magnitude == 0F)
        {
            return zero;
        }

        uint bits = BitConverter.SingleToUInt32Bits(magnitude);
        bool subnormal = bits < MinimumNormalBits;
        if (subnormal)
        {
            // Multiplication by 2^24 is exact and makes every nonzero binary32 subnormal normal.
            // Correcting its exponent below preserves the original magnitude and relative bin spacing.
            bits = BitConverter.SingleToUInt32Bits(magnitude * SubnormalScale);
        }

        // The exponent selects a doubling interval. Its leading fraction bits select a bin within
        // that interval. Subtract the minimum exponent to number intervals from the smallest subnormal.
        int exponent = (int)(bits >> FractionWidth) - 127 - (subnormal ? SubnormalExponentAdjustment : 0);
        int fraction = (int)((bits & FractionMask) >> (FractionWidth - this.fractionBits));

        // Reserve offset zero for exact zero. Each nonzero magnitude lies at least one bin away.
        int offset = ((exponent - MinimumExponent) << this.fractionBits) + fraction + 1;
        return value < 0F ? zero - offset : zero + offset;
    }

    /// <summary>
    /// Computes measurements and bin indices in one row traversal. Alpha does not enter the measurement.
    /// </summary>
    /// <param name="source">Straight source components from the pixel conversion API.</param>
    /// <param name="weights">The caller's RGB measurement coefficients.</param>
    /// <param name="destination">One index for each source value.</param>
    public void Invoke(ReadOnlySpan<Vector4> source, Vector3 weights, Span<int> destination)
    {
        ref int target = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint blocks = (source.Length - i).Vector512Count<float>();
            for (nuint block = 0; block < blocks; block++, i += Vector512<float>.Count)
            {
                // Deinterleaving puts one pixel's corresponding RGB components in each lane.
                // Match scalar multiplication and addition order; do not fuse the measurement arithmetic.
                SimdUtils.Deinterleave4(source, i, out Vector512<float> r, out Vector512<float> g, out Vector512<float> b, out _);
                Vector512<float> measurement = ((r * Vector512.Create(weights.X)) + (g * Vector512.Create(weights.Y))) + (b * Vector512.Create(weights.Z));
                this.Invoke(measurement).StoreUnsafe(ref target, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint blocks = (source.Length - i).Vector256Count<float>();
            for (nuint block = 0; block < blocks; block++, i += Vector256<float>.Count)
            {
                // Deinterleaving puts one pixel's corresponding RGB components in each lane.
                // Match scalar multiplication and addition order; do not fuse the measurement arithmetic.
                SimdUtils.Deinterleave4(source, i, out Vector256<float> r, out Vector256<float> g, out Vector256<float> b, out _);
                Vector256<float> measurement = ((r * Vector256.Create(weights.X)) + (g * Vector256.Create(weights.Y))) + (b * Vector256.Create(weights.Z));
                this.Invoke(measurement).StoreUnsafe(ref target, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint blocks = (source.Length - i).Vector128Count<float>();
            for (nuint block = 0; block < blocks; block++, i += Vector128<float>.Count)
            {
                // Deinterleaving puts one pixel's corresponding RGB components in each lane.
                // Match scalar multiplication and addition order; do not fuse the measurement arithmetic.
                SimdUtils.Deinterleave4(source, i, out Vector128<float> r, out Vector128<float> g, out Vector128<float> b, out _);
                Vector128<float> measurement = ((r * Vector128.Create(weights.X)) + (g * Vector128.Create(weights.Y))) + (b * Vector128.Create(weights.Z));
                this.Invoke(measurement).StoreUnsafe(ref target, (uint)i);
            }
        }

        // Each remaining pixel uses the same arithmetic and bin boundaries as the SIMD lanes.
        for (; i < source.Length; i++)
        {
            Vector4 value = source[i];
            destination[i] = this.Invoke(((value.X * weights.X) + (value.Y * weights.Y)) + (value.Z * weights.Z));
        }
    }

    /// <summary>
    /// Maps independent measurement lanes to ordered integer bin indices.
    /// </summary>
    /// <param name="value">Finite measurements within [0,1] for SDR, or within the finite float range for extended input.</param>
    /// <returns>One bin index per input lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector128<int> Invoke(Vector128<float> value)
    {
        if (this.fractionBits < 0)
        {
            // SDR pixel conversion and RGB measurement already supply finite lanes in [0,1].
            return Vector128.ConvertToInt32(Vector128.Round(value * Vector128.Create((float)(this.BinCount - 1))));
        }

        // Read exponent and fraction fields from positive IEEE 754 values. Multiply only subnormal
        // lanes by 2^24 to expose their exponent, then subtract that adjustment from those same lanes.
        Vector128<float> magnitude = Vector128.Abs(value);
        Vector128<uint> small = Vector128.LessThan(magnitude.AsUInt32(), Vector128.Create(MinimumNormalBits));
        Vector128<float> normal = Vector128.ConditionalSelect(small.AsSingle(), magnitude * Vector128.Create(SubnormalScale), magnitude);
        Vector128<int> bits = normal.AsInt32();

        // Each lane retains its own exponent and leading fraction bits. No horizontal reduction,
        // gather or logarithm is needed, and an exceptional neighbor cannot alter another lane.
        Vector128<int> exponent = (bits >>> FractionWidth) - Vector128.Create(127 + MinimumExponent);
        exponent -= Vector128.ConditionalSelect(small.AsInt32(), Vector128.Create(SubnormalExponentAdjustment), Vector128<int>.Zero);
        Vector128<int> fraction = (bits & Vector128.Create((int)FractionMask)) >>> (FractionWidth - this.fractionBits);
        Vector128<int> offset = (exponent << this.fractionBits) + fraction + Vector128<int>.One;

        // Zero also matches the subnormal mask. Restore its zero offset before selecting the sign,
        // so positive and negative zero both map to the single center bin.
        offset = Vector128.ConditionalSelect(Vector128.Equals(magnitude, Vector128<float>.Zero).AsInt32(), Vector128<int>.Zero, offset);

        Vector128<int> signed = Vector128.ConditionalSelect(Vector128.LessThan(value, Vector128<float>.Zero).AsInt32(), -offset, offset);
        return Vector128.Create(this.BinCount / 2) + signed;
    }

    /// <summary>
    /// Maps independent measurement lanes to ordered integer bin indices.
    /// </summary>
    /// <param name="value">Finite measurements within [0,1] for SDR, or within the finite float range for extended input.</param>
    /// <returns>One bin index per input lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector256<int> Invoke(Vector256<float> value)
    {
        if (this.fractionBits < 0)
        {
            // SDR pixel conversion and RGB measurement already supply finite lanes in [0,1].
            return Vector256.ConvertToInt32(Vector256.Round(value * Vector256.Create((float)(this.BinCount - 1))));
        }

        // Read exponent and fraction fields from positive IEEE 754 values. Multiply only subnormal
        // lanes by 2^24 to expose their exponent, then subtract that adjustment from those same lanes.
        Vector256<float> magnitude = Vector256.Abs(value);
        Vector256<uint> small = Vector256.LessThan(magnitude.AsUInt32(), Vector256.Create(MinimumNormalBits));
        Vector256<float> normal = Vector256.ConditionalSelect(small.AsSingle(), magnitude * Vector256.Create(SubnormalScale), magnitude);
        Vector256<int> bits = normal.AsInt32();

        // Each lane retains its own exponent and leading fraction bits. No horizontal reduction,
        // gather or logarithm is needed, and an exceptional neighbor cannot alter another lane.
        Vector256<int> exponent = (bits >>> FractionWidth) - Vector256.Create(127 + MinimumExponent);
        exponent -= Vector256.ConditionalSelect(small.AsInt32(), Vector256.Create(SubnormalExponentAdjustment), Vector256<int>.Zero);
        Vector256<int> fraction = (bits & Vector256.Create((int)FractionMask)) >>> (FractionWidth - this.fractionBits);
        Vector256<int> offset = (exponent << this.fractionBits) + fraction + Vector256<int>.One;

        // Zero also matches the subnormal mask. Restore its zero offset before selecting the sign,
        // so positive and negative zero both map to the single center bin.
        offset = Vector256.ConditionalSelect(Vector256.Equals(magnitude, Vector256<float>.Zero).AsInt32(), Vector256<int>.Zero, offset);

        Vector256<int> signed = Vector256.ConditionalSelect(Vector256.LessThan(value, Vector256<float>.Zero).AsInt32(), -offset, offset);
        return Vector256.Create(this.BinCount / 2) + signed;
    }

    /// <summary>
    /// Maps independent measurement lanes to ordered integer bin indices.
    /// </summary>
    /// <param name="value">Finite measurements within [0,1] for SDR, or within the finite float range for extended input.</param>
    /// <returns>One bin index per input lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector512<int> Invoke(Vector512<float> value)
    {
        if (this.fractionBits < 0)
        {
            // SDR pixel conversion and RGB measurement already supply finite lanes in [0,1].
            return Vector512.ConvertToInt32(Vector512.Round(value * Vector512.Create((float)(this.BinCount - 1))));
        }

        // Read exponent and fraction fields from positive IEEE 754 values. Multiply only subnormal
        // lanes by 2^24 to expose their exponent, then subtract that adjustment from those same lanes.
        Vector512<float> magnitude = Vector512.Abs(value);
        Vector512<uint> small = Vector512.LessThan(magnitude.AsUInt32(), Vector512.Create(MinimumNormalBits));
        Vector512<float> normal = Vector512.ConditionalSelect(small.AsSingle(), magnitude * Vector512.Create(SubnormalScale), magnitude);
        Vector512<int> bits = normal.AsInt32();

        // Each lane retains its own exponent and leading fraction bits. No horizontal reduction,
        // gather or logarithm is needed, and an exceptional neighbor cannot alter another lane.
        Vector512<int> exponent = (bits >>> FractionWidth) - Vector512.Create(127 + MinimumExponent);
        exponent -= Vector512.ConditionalSelect(small.AsInt32(), Vector512.Create(SubnormalExponentAdjustment), Vector512<int>.Zero);
        Vector512<int> fraction = (bits & Vector512.Create((int)FractionMask)) >>> (FractionWidth - this.fractionBits);
        Vector512<int> offset = (exponent << this.fractionBits) + fraction + Vector512<int>.One;

        // Zero also matches the subnormal mask. Restore its zero offset before selecting the sign,
        // so positive and negative zero both map to the single center bin.
        offset = Vector512.ConditionalSelect(Vector512.Equals(magnitude, Vector512<float>.Zero).AsInt32(), Vector512<int>.Zero, offset);

        Vector512<int> signed = Vector512.ConditionalSelect(Vector512.LessThan(value, Vector512<float>.Zero).AsInt32(), -offset, offset);
        return Vector512.Create(this.BinCount / 2) + signed;
    }
}
