// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Fields;

/// <summary>
/// Unsigned 64-bit variable-length coding.
/// </summary>
internal static class JxlU64Coder
{
    /// <summary>
    /// Reads the variable-length, unsigned 64-bit integer.
    /// </summary>
    public static ulong Read(JxlBitReader reader)
    {
        uint selector = reader.ReadBits32(2u);

        if (selector == 0u)
        {
            return 0u;
        }
        else if (selector == 1u)
        {
            return 1u + reader.ReadBits32(4u);
        }
        else if (selector == 2u)
        {
            return 17u + reader.ReadBits32(8u);
        }

        // Selector 3...
        ulong result = reader.ReadBits32(12u);
        int shift = 12;

        while (reader.ReadBoolean())
        {
            if (shift == 60)
            {
                result |= (ulong)reader.ReadBits32(4u) << shift;
                break;
            }

            result |= (ulong)reader.ReadBits32(8u) << shift;
            shift += 8;
        }

        return result;
    }

    /// <summary>
    /// Returns a value indicating whether can the value be encoded,
    /// as well as the number of encoded bits.
    /// </summary>
    public static bool CanEncode(ulong value, ref int encodedBits)
    {
        if (value == 0)
        {
            // 2 selector bits
            encodedBits = 2;
        }
        else if (value <= 16)
        {
            // 2 selector bits + 4 payload bits
            encodedBits = 2 + 4;
        }
        else if (value <= 272)
        {
            // 2 selector bits + 8 payload bits
            encodedBits = 2 + 8;
        }
        else
        {
            // 2 selector bits + 12 payload bits
            encodedBits = 2 + 12;
            value >>= 12;
            int shift = 12;
            while (value > 0 && shift < 60)
            {
                // 1 continuation bit + 8 payload bits
                encodedBits += 1 + 8;
                value >>= 8;
                shift += 8;
            }

            if (value > 0)
            {
                // 1 continuation bit + 4 payload bits
                encodedBits += 1 + 4;
            }
            else
            {
                // 1 stop bit
                encodedBits += 1;
            }
        }

        return true;
    }

    /// <summary>
    /// Always returns 73.
    /// </summary>
    public static int MaxEncodedBits() => 73;
}
