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
    public static void GetIntermediateRounding(int bitDepth, out int roundBits, out int roundOffset)
    {
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
    public static Vector128<ushort> FinalizeIntermediate(Vector128<ushort> value, int roundBits, int roundOffset)
    {
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
    /// Removes the compound bias and final fractional precision from 128-bit widened lanes.
    /// </summary>
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
