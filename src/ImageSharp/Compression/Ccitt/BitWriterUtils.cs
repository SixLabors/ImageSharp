// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

    /// <summary>
    /// Packs a row of one byte for each pixel into 1-bit pixels. A pixel is a 1 bit when its byte is not
    /// <paramref name="zero"/>.
    /// </summary>
    /// <param name="pixels">The pixels. Each byte is 0 or 255.</param>
    /// <param name="zero">The byte value, 0 or 255, that gives a 0 bit.</param>
    /// <param name="row">The destination row. It holds at least (pixels.Length + 7) / 8 bytes.</param>
    public static void PackBytes(ReadOnlySpan<byte> pixels, byte zero, Span<byte> row)
    {
        // An XOR with the zero value gives 0x00 for a 0 bit and 0xFF for a 1 bit, so the most significant bit of each
        // byte is the pixel bit.
        ulong flip = zero == 0 ? 0 : ulong.MaxValue;
        ref byte source = ref MemoryMarshal.GetReference(pixels);
        int whole = pixels.Length >> 3;

        for (int i = 0; i < whole; i++)
        {
            // Eight pixels at a time. The byte swap puts pixel 0 in the high byte. The mask keeps the most significant bit
            // of each byte. The multiply moves the bit of byte k (k = 0 for the low byte) to bit 56 + k. It makes no
            // carries, because each product term lands on a different bit. So the high byte has pixel 0 in its most
            // significant bit, as the row layout needs.
            ulong eight = BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref source, i << 3))) ^ flip;
            row[i] = (byte)(((eight & 0x8080808080808080UL) * 0x0002040810204081UL) >> 56);
        }

        // The last pixels fill the high bits of the last byte.
        int remainder = pixels.Length & 7;
        if (remainder > 0)
        {
            int last = 0;
            for (int i = 0; i < remainder; i++)
            {
                if (Unsafe.Add(ref source, (whole << 3) + i) != zero)
                {
                    last |= 0x80 >> i;
                }
            }

            row[whole] = (byte)last;
        }
    }
}
