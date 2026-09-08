// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg;

/// <summary>
/// Decodes Huffman codes in a JPEG file for JPEG to JPEG XL encoder.
/// </summary>
internal static class JpegHuffmanDecoder
{
    public const int RootTableBits = 8;
    public const int LookupSize = 8;

    private static int NextTableBitSize(Span<int> count, int length)
    {
        int left = 1 << (length - RootTableBits);
        while (length < MaxBitLength)
        {
            left -= count[length];

            if (left <= 0)
            {
                break;
            }

            length++;
            left <<= 1;
        }

        return length - RootTableBits;
    }

    public struct HuffmanTableEntry()
    {
        public byte Bits = 0;
        public ushort Value = 0xFFFF;
    }
}
