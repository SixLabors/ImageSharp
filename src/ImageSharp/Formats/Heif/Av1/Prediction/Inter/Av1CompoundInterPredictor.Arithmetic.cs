// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Provides shared final-rounding arithmetic for compound intermediate reconstruction.
/// </content>
internal static partial class Av1CompoundInterPredictor
{
    /// <summary>
    /// Derives the bias and remaining fractional precision of a compound intermediate.
    /// </summary>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="roundBits">The fractional bits that remain in the intermediate after both convolution passes.</param>
    /// <param name="roundOffset">The bias that the convolution adds to keep every intermediate value positive.</param>
    public static void GetIntermediateRounding(int bitDepth, out int roundBits, out int roundOffset)
    {
        // The literals are the filter precision (7), the default first-pass shift (3) and the full two-pass precision (14).
        // For 12-bit content the first pass shifts 2 more bits, so the horizontal intermediate stays within 16 bits.
        int intermediateRange = bitDepth + 7 - 3 + 2;
        int round0 = 3 + Math.Max(intermediateRange - 16, 0);
        int offsetBits = bitDepth + 14 - round0;
        roundBits = 14 - round0 - CompoundRound1Bits;
        roundOffset = (1 << (offsetBits - CompoundRound1Bits)) +
            (1 << (offsetBits - CompoundRound1Bits - 1));
    }

    /// <summary>
    /// Removes the compound bias and final fractional precision from 128-bit unsigned lanes.
    /// </summary>
    /// <param name="value">The biased 8-bit intermediate samples.</param>
    /// <param name="roundBits">The fractional bits to remove.</param>
    /// <param name="roundOffset">The bias to remove.</param>
    /// <returns>The 8-bit samples, clamped to 0 through 255, in 16-bit lanes.</returns>
    public static Vector128<ushort> FinalizeIntermediate(Vector128<ushort> value, int roundBits, int roundOffset)
    {
        // For 8-bit content the unbiased value fits in a signed 16-bit lane. Thus the wrapped unsigned subtraction reads back as the signed difference.
        Vector128<short> result = (value - Vector128.Create((ushort)roundOffset)).AsInt16();
        if (roundBits != 0)
        {
            result = (result + Vector128.Create((short)(1 << (roundBits - 1)))) >> roundBits;
        }

        result = Vector128.Max(Vector128<short>.Zero, Vector128.Min(Vector128.Create((short)byte.MaxValue), result));
        return result.AsUInt16();
    }

    /// <summary>
    /// Removes the compound bias and final fractional precision from 256-bit unsigned lanes.
    /// </summary>
    /// <param name="value">The biased 8-bit intermediate samples.</param>
    /// <param name="roundBits">The fractional bits to remove.</param>
    /// <param name="roundOffset">The bias to remove.</param>
    /// <returns>The 8-bit samples, clamped to 0 through 255, in 16-bit lanes.</returns>
    public static Vector256<ushort> FinalizeIntermediate(Vector256<ushort> value, int roundBits, int roundOffset)
    {
        Vector256<short> result = (value - Vector256.Create((ushort)roundOffset)).AsInt16();
        if (roundBits != 0)
        {
            result = (result + Vector256.Create((short)(1 << (roundBits - 1)))) >> roundBits;
        }

        result = Vector256.Max(Vector256<short>.Zero, Vector256.Min(Vector256.Create((short)byte.MaxValue), result));
        return result.AsUInt16();
    }

    /// <summary>
    /// Removes the compound bias and final fractional precision from 512-bit unsigned lanes.
    /// </summary>
    /// <param name="value">The biased 8-bit intermediate samples.</param>
    /// <param name="roundBits">The fractional bits to remove.</param>
    /// <param name="roundOffset">The bias to remove.</param>
    /// <returns>The 8-bit samples, clamped to 0 through 255, in 16-bit lanes.</returns>
    public static Vector512<ushort> FinalizeIntermediate(Vector512<ushort> value, int roundBits, int roundOffset)
    {
        Vector512<short> result = (value - Vector512.Create((ushort)roundOffset)).AsInt16();
        if (roundBits != 0)
        {
            result = (result + Vector512.Create((short)(1 << (roundBits - 1)))) >> roundBits;
        }

        result = Vector512.Max(Vector512<short>.Zero, Vector512.Min(Vector512.Create((short)byte.MaxValue), result));
        return result.AsUInt16();
    }

    /// <summary>
    /// Removes compound bias and fractional precision from 128-bit high-bit-depth lanes.
    /// </summary>
    /// <param name="value">The biased high-bit-depth intermediate samples.</param>
    /// <param name="roundBits">The fractional bits to remove.</param>
    /// <param name="roundOffset">The bias to remove.</param>
    /// <param name="maximum">The largest sample value at the current bit depth.</param>
    /// <returns>The samples, clamped to 0 through <paramref name="maximum"/>.</returns>
    public static Vector128<ushort> FinalizeHighBitDepthIntermediate(
        Vector128<ushort> value,
        int roundBits,
        int roundOffset,
        int maximum)
    {
        // For 12-bit content, the bias removal can leave a value outside the signed 16-bit range. Thus the subtraction, rounding and clip use 32-bit lanes.
        // The two halves narrow back to unsigned 16-bit lanes only after the clip.
        (Vector128<uint> lower, Vector128<uint> upper) = Vector128.Widen(value);
        Vector128<int> bias = Vector128.Create(roundOffset);
        Vector128<int> lowerResult = lower.AsInt32() - bias;
        Vector128<int> upperResult = upper.AsInt32() - bias;
        if (roundBits != 0)
        {
            Vector128<int> rounding = Vector128.Create(1 << (roundBits - 1));
            lowerResult = (lowerResult + rounding) >> roundBits;
            upperResult = (upperResult + rounding) >> roundBits;
        }

        Vector128<int> ceiling = Vector128.Create(maximum);
        lowerResult = Vector128.Max(Vector128<int>.Zero, Vector128.Min(ceiling, lowerResult));
        upperResult = Vector128.Max(Vector128<int>.Zero, Vector128.Min(ceiling, upperResult));
        return Vector128.Narrow(lowerResult.AsUInt32(), upperResult.AsUInt32());
    }

    /// <summary>
    /// Removes compound bias and fractional precision from 256-bit high-bit-depth lanes.
    /// </summary>
    /// <param name="value">The biased high-bit-depth intermediate samples.</param>
    /// <param name="roundBits">The fractional bits to remove.</param>
    /// <param name="roundOffset">The bias to remove.</param>
    /// <param name="maximum">The largest sample value at the current bit depth.</param>
    /// <returns>The samples, clamped to 0 through <paramref name="maximum"/>.</returns>
    public static Vector256<ushort> FinalizeHighBitDepthIntermediate(
        Vector256<ushort> value,
        int roundBits,
        int roundOffset,
        int maximum)
    {
        // For 12-bit content, the bias removal can leave a value outside the signed 16-bit range. Thus the subtraction, rounding and clip use 32-bit lanes.
        // The two halves narrow back to unsigned 16-bit lanes only after the clip.
        (Vector256<uint> lower, Vector256<uint> upper) = Vector256.Widen(value);
        Vector256<int> bias = Vector256.Create(roundOffset);
        Vector256<int> lowerResult = lower.AsInt32() - bias;
        Vector256<int> upperResult = upper.AsInt32() - bias;
        if (roundBits != 0)
        {
            Vector256<int> rounding = Vector256.Create(1 << (roundBits - 1));
            lowerResult = (lowerResult + rounding) >> roundBits;
            upperResult = (upperResult + rounding) >> roundBits;
        }

        Vector256<int> ceiling = Vector256.Create(maximum);
        lowerResult = Vector256.Max(Vector256<int>.Zero, Vector256.Min(ceiling, lowerResult));
        upperResult = Vector256.Max(Vector256<int>.Zero, Vector256.Min(ceiling, upperResult));
        return Vector256.Narrow(lowerResult.AsUInt32(), upperResult.AsUInt32());
    }

    /// <summary>
    /// Removes compound bias and fractional precision from 512-bit high-bit-depth lanes.
    /// </summary>
    /// <param name="value">The biased high-bit-depth intermediate samples.</param>
    /// <param name="roundBits">The fractional bits to remove.</param>
    /// <param name="roundOffset">The bias to remove.</param>
    /// <param name="maximum">The largest sample value at the current bit depth.</param>
    /// <returns>The samples, clamped to 0 through <paramref name="maximum"/>.</returns>
    public static Vector512<ushort> FinalizeHighBitDepthIntermediate(
        Vector512<ushort> value,
        int roundBits,
        int roundOffset,
        int maximum)
    {
        // For 12-bit content, the bias removal can leave a value outside the signed 16-bit range. Thus the subtraction, rounding and clip use 32-bit lanes.
        // The two halves narrow back to unsigned 16-bit lanes only after the clip.
        (Vector512<uint> lower, Vector512<uint> upper) = Vector512.Widen(value);
        Vector512<int> bias = Vector512.Create(roundOffset);
        Vector512<int> lowerResult = lower.AsInt32() - bias;
        Vector512<int> upperResult = upper.AsInt32() - bias;
        if (roundBits != 0)
        {
            Vector512<int> rounding = Vector512.Create(1 << (roundBits - 1));
            lowerResult = (lowerResult + rounding) >> roundBits;
            upperResult = (upperResult + rounding) >> roundBits;
        }

        Vector512<int> ceiling = Vector512.Create(maximum);
        lowerResult = Vector512.Max(Vector512<int>.Zero, Vector512.Min(ceiling, lowerResult));
        upperResult = Vector512.Max(Vector512<int>.Zero, Vector512.Min(ceiling, upperResult));
        return Vector512.Narrow(lowerResult.AsUInt32(), upperResult.AsUInt32());
    }

    /// <summary>
    /// Removes the compound bias and final fractional precision from 128-bit widened lanes.
    /// </summary>
    /// <param name="value">The biased 8-bit intermediate samples in 32-bit lanes.</param>
    /// <param name="roundBits">The fractional bits to remove.</param>
    /// <param name="roundOffset">The bias to remove.</param>
    /// <returns>The 8-bit samples, clamped to 0 through 255, in 32-bit lanes.</returns>
    public static Vector128<int> FinalizeIntermediate(Vector128<int> value, int roundBits, int roundOffset)
    {
        Vector128<int> result = value - Vector128.Create(roundOffset);
        if (roundBits != 0)
        {
            result = (result + Vector128.Create(1 << (roundBits - 1))) >> roundBits;
        }

        return Vector128.Max(Vector128<int>.Zero, Vector128.Min(Vector128.Create((int)byte.MaxValue), result));
    }

    /// <summary>
    /// Removes the compound bias and final fractional precision from 256-bit widened lanes.
    /// </summary>
    /// <param name="value">The biased 8-bit intermediate samples in 32-bit lanes.</param>
    /// <param name="roundBits">The fractional bits to remove.</param>
    /// <param name="roundOffset">The bias to remove.</param>
    /// <returns>The 8-bit samples, clamped to 0 through 255, in 32-bit lanes.</returns>
    public static Vector256<int> FinalizeIntermediate(Vector256<int> value, int roundBits, int roundOffset)
    {
        Vector256<int> result = value - Vector256.Create(roundOffset);
        if (roundBits != 0)
        {
            result = (result + Vector256.Create(1 << (roundBits - 1))) >> roundBits;
        }

        return Vector256.Max(Vector256<int>.Zero, Vector256.Min(Vector256.Create((int)byte.MaxValue), result));
    }

    /// <summary>
    /// Removes the compound bias and final fractional precision from 512-bit widened lanes.
    /// </summary>
    /// <param name="value">The biased 8-bit intermediate samples in 32-bit lanes.</param>
    /// <param name="roundBits">The fractional bits to remove.</param>
    /// <param name="roundOffset">The bias to remove.</param>
    /// <returns>The 8-bit samples, clamped to 0 through 255, in 32-bit lanes.</returns>
    public static Vector512<int> FinalizeIntermediate(Vector512<int> value, int roundBits, int roundOffset)
    {
        Vector512<int> result = value - Vector512.Create(roundOffset);
        if (roundBits != 0)
        {
            result = (result + Vector512.Create(1 << (roundBits - 1))) >> roundBits;
        }

        return Vector512.Max(Vector512<int>.Zero, Vector512.Min(Vector512.Create((int)byte.MaxValue), result));
    }
}
