// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// Unsigned 32-bit variable-length integer coder.
/// </summary>
internal static class JxlU32Coder
{
    /// <summary>
    /// Maximum number of writeable and/or readable bits in a variable-length integer.
    /// </summary>
    public static int MaxEncodedBits(in JxlU32Enc enc)
    {
        int extraBits = 0;

        for (int selector = 0; selector < 4; selector++)
        {
            JxlU32Distribution distr = enc.GetDistribution(selector);

            if (distr.IsDirect)
            {
                continue;
            }
            else
            {
                extraBits = Math.Max(extraBits, (int)distr.ExtraBits);
            }
        }

        return 2 + extraBits;
    }

    /// <summary>
    /// Verifies that the value can be encoded.
    /// </summary>
    public static bool CanEncode(in JxlU32Enc enc, uint value, ref int encodedBits)
    {
        uint selector = 0;
        int totalBits = 0;

        bool isOk = ChooseSelector(in enc, value, ref selector, ref totalBits);

        encodedBits = isOk ? totalBits : 0;

        return isOk;
    }

    /// <summary>
    /// Reads the U32 coded value.
    /// </summary>
    public static uint Read(in JxlU32Enc enc, JxlBitReader reader)
    {
        uint selector = reader.ReadBits32(2u);
        JxlU32Distribution dist = enc.GetDistribution((int)selector);

        if (dist.IsDirect)
        {
            return dist.Direct;
        }
        else
        {
            return reader.ReadBits32(dist.ExtraBits) + dist.Offset;
        }
    }

    /// <summary>
    /// Tries to find the best one of the four selectors based on the value.
    /// </summary>
    /// <param name="enc">The four wire alternatives.</param>
    /// <param name="value">The unsigned value to encode.</param>
    /// <param name="selector">The selected alternative.</param>
    /// <param name="totalBits">The selected field length, including the two selector bits.</param>
    /// <returns>Whether at least one alternative can represent the value.</returns>
    public static bool ChooseSelector(in JxlU32Enc enc, uint value, ref uint selector, ref int totalBits)
    {
        selector = 0;

        // Alternatives use at most 32 payload bits plus two selector bits. The larger sentinel cannot be a valid selected length.
        totalBits = 64;

        for (int s = 0; s < 4; s++)
        {
            JxlU32Distribution dist = enc.GetDistribution(s);

            if (dist.IsDirect)
            {
                if (dist.Direct == value)
                {
                    selector = (uint)s;
                    totalBits = 2;
                    return true;
                }

                continue;
            }

            uint extraBits = dist.ExtraBits;
            uint offset = dist.Offset;

            // Check the payload distance after the offset, without wrapping either the endpoint or a 32-bit shift.
            // Full-width alternatives can represent every UInt32 at or above their offset.
            if (value < offset || (ulong)(value - offset) >= (1UL << (int)extraBits))
            {
                continue;
            }

            if (2 + extraBits < totalBits)
            {
                selector = (uint)s;
                totalBits = 2 + (int)extraBits;
            }
        }

        return totalBits != 64;
    }

    /// <summary>
    /// Writes the shortest matching alternative and its offset-adjusted payload.
    /// </summary>
    /// <param name="enc">The four wire alternatives.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="writer">The output bit writer.</param>
    /// <returns>Whether an alternative can represent the value. An invalid value writes no bits.</returns>
    public static bool Write(in JxlU32Enc enc, uint value, JxlBitWriter writer)
    {
        uint selector = 0;
        int totalBits = 0;

        if (!ChooseSelector(in enc, value, ref selector, ref totalBits))
        {
            return false;
        }

        writer.Write(2, selector);

        JxlU32Distribution d = enc.GetDistribution((int)selector);

        if (!d.IsDirect)
        {
            uint offset = d.Offset;

            // ChooseSelector already checked this subtraction and selected the exact payload width.
            writer.Write(totalBits - 2, value - offset);
        }

        return true;
    }
}
