// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

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
    public static bool ChooseSelector(in JxlU32Enc enc, uint value, ref uint selector, ref int totalBits)
    {
        int bitsRequired = 32 - JxlMath.Num0BitsAboveMS1Bit(value);

        if (bitsRequired > 32)
        {
            return false;
        }

        selector = 0;
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

            if (value < offset || value >= offset + (1u << (int)extraBits))
            {
                continue;
            }

            if (2 + extraBits < totalBits)
            {
                selector = (uint)s;
                totalBits = 2 + (int)extraBits;
            }
        }

        if (totalBits == 64)
        {
            DebugGuard.IsTrue(false, "No matching selector");

            return false;
        }

        return true;
    }
}
