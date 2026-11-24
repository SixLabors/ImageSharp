// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Compression.Ccitt;

/// <summary>
/// Writes 1-bit pixels to rows. The first pixel of a row is the most significant bit of its first byte.
/// </summary>
internal static class BitWriterUtils
{
    /// <summary>
    /// Sets a range of bits to 1.
    /// </summary>
    /// <param name="row">The destination row.</param>
    /// <param name="start">The first bit.</param>
    /// <param name="count">The number of bits.</param>
    public static void SetBits(Span<byte> row, int start, int count)
    {
        if (count == 0)
        {
            return;
        }

        int end = start + count;
        int first = start >> 3;
        int last = (end - 1) >> 3;

        // The first byte keeps its bits before the start: 0xFF >> (start mod 8) selects the bits from the start. The last
        // byte keeps its bits after the end: 0xFF << (8 - (end mod 8)) selects the bits before the end.
        byte firstMask = (byte)(0xFF >> (start & 7));
        byte lastMask = (byte)(0xFF << ((8 - (end & 7)) & 7));

        if (first == last)
        {
            row[first] |= (byte)(firstMask & lastMask);
            return;
        }

        row[first] |= firstMask;
        row[(first + 1)..last].Fill(0xFF);
        row[last] |= lastMask;
    }
}
