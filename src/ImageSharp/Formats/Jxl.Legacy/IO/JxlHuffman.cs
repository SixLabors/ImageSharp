// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

namespace SixLabors.ImageSharp.Formats.Jxl.IO;

/// <summary>
/// Shared Huffman I/O utilities.
/// </summary>
internal static class JxlHuffman
{
    /// <summary>
    /// Returns Reverse(Reverse(Key, Len) + 1, Len). The
    /// Reverse(Key, Len) function performs bitwise reversal
    /// of the len least significant bits of the key value.
    /// </summary>
    public static uint GetNextKey(uint key, int len)
    {
        uint step = 1u << (len - 1);
        while ((key & step) != 0)
        {
            step >>= 1;
        }

        return (key & (step - 1)) + step;
    }

    /// <summary>
    /// Replicates <paramref name="code"/> into <paramref name="table"/> every <paramref name="step"/>
    /// times with the upper bound of <paramref name="end"/>.
    /// </summary>
    public static void ReplicateValue(Span<JxlHuffmanCode> table, int step, int end, JxlHuffmanCode code)
    {
        do
        {
            end -= step;
            table[end] = code;
        }
        while (end > 0);
    }

    /// <summary>
    /// Returns the table width of the next 2nd level table.
    /// </summary>
    /// <param name="count">The histogram of bit lengths for remaining symbols</param>
    /// <param name="length">Code length of the next processed symbol</param>
    /// <param name="rootBits">Amount of bits for the root symbol</param>
    /// <returns>Table width for the 2nd level table.</returns>
    public static int NextTableBitSize(ReadOnlySpan<ushort> count, int length, int rootBits)
    {
        uint left = 1u << (length - rootBits);

        while (length < JxlAnsConstants.PrefixMaxBits)
        {
            if (left <= count[length])
            {
                break;
            }

            left -= count[length];
            length++;
            left <<= 1;
        }

        return length - rootBits;
    }

    public static uint BuildHuffmanTable(
        Span<JxlHuffmanCode> rootTable,
        int rootBits,
        ReadOnlySpan<byte> codeLengths,
        Span<ushort> count)
    {
        if (codeLengths.Length > (1u << JxlAnsConstants.PrefixMaxBits))
        {
            return 0u;
        }

        Span<ushort> offset = stackalloc ushort[JxlAnsConstants.PrefixMaxBits + 1];

        Span<ushort> sortedStorage = stackalloc ushort[codeLengths.Length];

        int maxLength = 1;
        ushort sum = 0;
        int len, symbol;
        for (len = 1; len <= JxlAnsConstants.PrefixMaxBits; len++)
        {
            offset[len] = sum;

            if (count[len] != 0)
            {
                sum = (ushort)(sum + count[len]);
                maxLength = len;
            }
        }

        for (symbol = 0; symbol < codeLengths.Length; symbol++)
        {
            if (codeLengths[symbol] != 0)
            {
                sortedStorage[offset[codeLengths[symbol]]++] = (ushort)symbol;
            }
        }

        Span<JxlHuffmanCode> table = rootTable;
        int tableBits = rootBits;
        uint tableSize = 1u << tableBits;
        uint totalSize = tableSize;

        JxlHuffmanCode code = default;

        if (offset[JxlAnsConstants.PrefixMaxBits] == 1)
        {
            code.Bits = 0;
            code.Value = sortedStorage[0];

            for (int i = 0; i < totalSize; i++)
            {
                table[i] = code;
            }
        }

        if (tableBits > maxLength)
        {
            tableBits = maxLength;
            tableSize = 1u << tableBits;
        }

        int key = 0;
        code.Bits = 0;
        int step = 2;

        do
        {
            for (; count[code.Bits] != 0; --count[code.Bits])
            {
                code.Value = sortedStorage[symbol++];
                ReplicateValue(table[key..], step, (int)tableSize, code);
                key = (int)GetNextKey((uint)key, code.Bits);
            }

            step <<= 1;
        }
        while (++code.Bits <= tableBits);

        while (totalSize != tableSize)
        {
            table[..(int)tableSize].CopyTo(table[(int)tableSize..]);
            tableSize <<= 1;
        }

        uint mask = totalSize - 1u;
        int low = -1;

        uint tableOffset = 0;
        for (step = 2; len <= maxLength; len++, step <<= 1)
        {
            for (; count[len] != 0; --count[len])
            {
                if ((key & mask) != low)
                {
                    tableOffset += tableSize;
                    table = table[(int)tableSize..];
                    tableBits = NextTableBitSize(count, len, rootBits);
                    tableSize = 1u << tableBits;
                    totalSize += tableSize;
                    low = key & (int)mask;

                    rootTable[low].Bits = (byte)(tableBits + rootBits);
                    rootTable[low].Value = (ushort)(tableOffset - low);
                }

                code.Bits = (byte)(len - rootBits);
                code.Value = sortedStorage[symbol++];

                ReplicateValue(table[(key >> rootBits)..], step, (int)tableSize, code);
                key = (int)GetNextKey((uint)key, len);
            }
        }

        return totalSize;
    }
}
