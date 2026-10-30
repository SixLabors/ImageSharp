// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Compression;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Reads JPEG XL prefix-code descriptions into the shared canonical Huffman table layout.
/// </summary>
internal static class JxlHuffmanDecoder
{
    /// <summary>
    /// Reads the code length of each alphabet symbol into caller-owned storage.
    /// </summary>
    /// <param name="reader">The bounded codestream reader.</param>
    /// <param name="lengths">The destination, with one entry per alphabet symbol.</param>
    public static void ReadCodeLengths(ref JxlBitReader reader, Span<int> lengths)
    {
        lengths.Clear();
        if (lengths.Length == 1)
        {
            // A one-symbol alphabet needs no encoded tree. The shared builder represents it with zero-bit lookups.
            lengths[0] = 1;
            return;
        }

        int selector = (int)reader.ReadBits(2);
        if (selector == 1)
        {
            // The simple form names at most four symbols. The alphabet bounds the fixed width used
            // for each symbol index; the encoded ordering determines which symbols receive the shortest codes.
            int count = (int)reader.ReadBits(2) + 1;
            Span<int> symbols = stackalloc int[4];
            int width = 32 - BitOperations.LeadingZeroCount((uint)lengths.Length - 1);
            for (int i = 0; i < count; i++)
            {
                int symbol = (int)reader.ReadBits(width);
                if (symbol >= lengths.Length || symbols[..i].Contains(symbol))
                {
                    throw new InvalidImageContentException("The JPEG XL prefix code contains an invalid or repeated symbol.");
                }

                symbols[i] = symbol;
            }

            // Canonical ordering sorts symbols only within each code length. The encoded order selects
            // the shorter codes in the three-symbol and asymmetric four-symbol forms.
            bool asymmetric = count == 4 && reader.ReadBoolean();
            for (int i = 0; i < count; i++)
            {
                lengths[symbols[i]] = count switch
                {
                    1 or 2 => 1,
                    3 => i == 0 ? 1 : 2,
                    _ => asymmetric ? Math.Min(i + 1, 3) : 2
                };
            }

            return;
        }

        // The complex form first describes a code for the code lengths themselves. The selector skips
        // initial entries in this fixed order; four-bit lookahead can identify codes shorter than four bits.
        ReadOnlySpan<byte> order = [1, 2, 3, 4, 0, 5, 17, 6, 16, 7, 8, 9, 10, 11, 12, 13, 14, 15];
        ReadOnlySpan<byte> bitCounts = [2, 2, 2, 3, 2, 2, 2, 4, 2, 2, 2, 3, 2, 2, 2, 4];
        ReadOnlySpan<byte> values = [0, 4, 3, 2, 0, 4, 3, 1, 0, 4, 3, 2, 0, 4, 3, 5];
        Span<int> lengthCode = stackalloc int[18];
        lengthCode.Clear();
        int space = 32;
        int nonzero = 0;

        // Measure available prefix space in units of 1/32. A nonzero length consumes 32 >> length units.
        // A complete tree exhausts that space; a one-symbol code is the format's explicit exception.
        for (int i = selector; i < order.Length && space > 0; i++)
        {
            int index = (int)reader.PeekBits(4);
            reader.ConsumeBits(bitCounts[index]);
            int value = values[index];
            lengthCode[order[i]] = value;
            if (value != 0)
            {
                space -= 32 >> value;
                nonzero++;
            }
        }

        if (nonzero != 1 && space != 0)
        {
            throw new InvalidImageContentException("The JPEG XL code-length alphabet is incomplete or oversubscribed.");
        }

        Span<HuffmanCode> table = stackalloc HuffmanCode[32];
        Span<int> sorted = stackalloc int[18];
        if (HuffmanTableBuilder.Build(table, 5, lengthCode, sorted) == 0)
        {
            throw new InvalidImageContentException("The JPEG XL code-length alphabet is invalid.");
        }

        int position = 0;
        int previous = 8;
        int repeat = 0;
        int repeatLength = 0;
        space = 1 << HuffmanTableBuilder.MaximumCodeLength;

        // Decode the final alphabet with the small length-code table. Unwritten lengths remain zero,
        // so the stream can finish once the complete prefix space has been assigned.
        while (position < lengths.Length && space > 0)
        {
            HuffmanCode entry = table[(int)reader.PeekBits(5)];
            reader.ConsumeBits(entry.BitsUsed);
            int value = (int)entry.Value;
            if (value < 16)
            {
                repeat = 0;
                lengths[position++] = value;
                if (value != 0)
                {
                    previous = value;
                    space -= (1 << HuffmanTableBuilder.MaximumCodeLength) >> value;
                }
            }
            else
            {
                int width = value - 14;
                int nextLength = value == 16 ? previous : 0;
                if (nextLength != repeatLength)
                {
                    repeat = 0;
                    repeatLength = nextLength;
                }

                // Consecutive repeat codes extend a base-4 or base-8 run. Validate the expanded run
                // before writing or shifting again, so malformed input cannot overflow the recurrence.
                int oldRepeat = repeat;
                repeat = repeat == 0 ? 0 : (repeat - 2) << width;
                repeat += (int)reader.ReadBits(width) + 3;
                int count = repeat - oldRepeat;
                if (count > lengths.Length - position)
                {
                    throw new InvalidImageContentException("The JPEG XL code-length run exceeds its alphabet.");
                }

                lengths.Slice(position, count).Fill(repeatLength);
                position += count;
                if (repeatLength != 0)
                {
                    space -= count << (HuffmanTableBuilder.MaximumCodeLength - repeatLength);
                }
            }
        }

        if (space != 0)
        {
            throw new InvalidImageContentException("The JPEG XL prefix code is incomplete or oversubscribed.");
        }
    }
}
