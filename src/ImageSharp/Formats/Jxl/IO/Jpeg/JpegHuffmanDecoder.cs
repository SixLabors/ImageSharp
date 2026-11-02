// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg;

/// <summary>
/// Decodes Huffman codes in a JPEG file for JPEG to JPEG XL encoder.
/// </summary>
internal static class JpegHuffmanDecoder
{
    public const int RootTableBits = 8;
    public const int LookupSize = 1024;

    private const int MaxBitLength = JpegDataConstants.JpegHuffmanMaxBitLength;

    public static int NextTableBitSize(Span<int> count, int length)
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

    public static void BuildJpegHuffmanTable(Span<int> count, Span<int> symbols, Span<HuffmanTableEntry> lut)
    {
        HuffmanTableEntry code = default;

        Span<int> tempCount = stackalloc int[MaxBitLength + 1];
        tempCount.Clear();

        int totalCount = 0;

        for (int len = 1; len <= MaxBitLength; ++len)
        {
            tempCount[len] = count[len];
            totalCount += tempCount[len];
        }

        Span<HuffmanTableEntry> table = lut;
        int tableOffset = 0;

        int tableBits = RootTableBits;
        int tableSize = 1 << tableBits;

        int key;

        // Special case code, with one value.
        if (totalCount == 1)
        {
            code.Bits = 0;
            code.Value = (ushort)symbols[0];

            for (key = 0; key < tableSize; ++key)
            {
                table[key] = code;
            }

            return;
        }

        // Filling in the root table
        key = 0;
        int idx = 0;
        for (int len = 1; len <= RootTableBits; ++len)
        {
            for (; tempCount[len] > 0; --tempCount[len])
            {
                code.Bits = (byte)len;
                code.Value = (ushort)symbols[idx++];
                int reps = 1 << (RootTableBits - len);

                while (reps-- != 0)
                {
                    table[key++] = code;
                }
            }
        }

        // Fill 2nd level tables; add pointers to root table
        table = table[tableSize..];
        tableOffset += tableSize;
        tableSize = 0;
        int low = 0;

        for (int len = RootTableBits + 1; len <= MaxBitLength; ++len)
        {
            for (; tempCount[len] > 0; --tempCount[len])
            {
                // Start a new sub-table if the previous one is full.
                if (low >= tableSize)
                {
                    table = table[tableSize..];
                    tableOffset += tableSize;

                    tableBits = NextTableBitSize(tempCount, len);
                    tableSize = 1 << tableBits;
                    low = 0;
                    lut[key].Bits = (byte)(tableBits + RootTableBits);
                    lut[key].Value = (ushort)(tableOffset - key);
                    ++key;
                }

                code.Bits = (byte)(len - RootTableBits);
                code.Value = (ushort)symbols[idx++];
                int reps = 1 << (tableBits - code.Bits);

                while (reps-- != 0)
                {
                    table[low++] = code;
                }
            }
        }
    }

    public struct HuffmanTableEntry()
    {
        public byte Bits = 0;
        public ushort Value = 0xFFFF;
    }
}
