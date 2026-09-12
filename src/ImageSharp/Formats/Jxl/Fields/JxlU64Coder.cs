// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

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

    public static bool Write(ulong value, JxlBitWriter writer)
    {
        if (value == 0)
        {
            // Selector: use 0 bits, value 0
            writer.Write(2, 0);
        }
        else if (value <= 16)
        {
            // Selector: use 4 bits, value 1..16
            writer.Write(2, 1);
            writer.Write(4, value - 1);
        }
        else if (value <= 272)
        {
            // Selector: use 8 bits, value 17..272
            writer.Write(2, 2);
            writer.Write(8, value - 17);
        }
        else
        {
            // Selector: varint, first a 12-bit group, after that per 8-bit group.
            writer.Write(2, 3);
            writer.Write(12, value & 4095);
            value >>= 12;
            int shift = 12;
            while (value > 0 && shift < 60)
            {
                // Indicate varint not done
                writer.Write(1, 1);
                writer.Write(8, value & 255);
                value >>= 8;
                shift += 8;
            }

            if (value > 0)
            {
                // This only could happen if shift == N - 4.
                writer.Write(1, 1);
                writer.Write(4, value & 15);

                // Implicitly closed sequence, no extra stop bit is required.
            }
            else
            {
                // Indicate end of varint
                writer.Write(1, 0);
            }
        }

        return true;
    }

    /// <summary>
    /// Always returns 73.
    /// </summary>
    public static int MaxEncodedBits() => 73;
}
