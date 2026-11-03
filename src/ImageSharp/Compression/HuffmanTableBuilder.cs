// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Compression;

/// <summary>
/// Builds least-significant-bit-first canonical Huffman lookup tables for lossless codecs.
/// </summary>
internal static class HuffmanTableBuilder
{
    /// <summary>
    /// The longest code accepted by the shared two-level table layout.
    /// </summary>
    public const int MaximumCodeLength = 15;

    /// <summary>
    /// Builds the table, or measures its required entry count when the destination is empty.
    /// </summary>
    /// <param name="table">The destination, or an empty span to measure it.</param>
    /// <param name="rootBits">The number of bits in the first lookup.</param>
    /// <param name="codeLengths">The code length for each alphabet symbol.</param>
    /// <param name="sorted">Caller-owned workspace with at least one entry per symbol.</param>
    /// <returns>The required entry count, or zero for an invalid code.</returns>
    public static int Build(Span<HuffmanCode> table, int rootBits, ReadOnlySpan<int> codeLengths, Span<int> sorted)
    {
        DebugGuard.MustBeGreaterThan(rootBits, 0, nameof(rootBits));
        int codeLengthsSize = codeLengths.Length;

        int totalSize = 1 << rootBits; // total size root table + 2nd level table.
        int len; // current code length.
        int symbol; // symbol index in original or sorted table.
        Span<int> counts = stackalloc int[MaximumCodeLength + 1]; // number of codes of each length.
        Span<int> offsets = stackalloc int[MaximumCodeLength + 1]; // offsets in sorted table for each length.

        counts.Clear();
        offsets.Clear();

        // Build histogram of code lengths.
        for (symbol = 0; symbol < codeLengthsSize; ++symbol)
        {
            int codeLengthOfSymbol = codeLengths[symbol];
            if (codeLengthOfSymbol > MaximumCodeLength)
            {
                return 0;
            }

            counts[codeLengthOfSymbol]++;
        }

        // Error, all code lengths are zeros.
        if (counts[0] == codeLengthsSize)
        {
            return 0;
        }

        // Generate offsets into sorted symbol table by code length.
        offsets[1] = 0;
        for (len = 1; len < MaximumCodeLength; ++len)
        {
            int codesOfLength = counts[len];
            if (codesOfLength > 1 << len)
            {
                return 0;
            }

            offsets[len + 1] = offsets[len] + codesOfLength;
        }

        // Sort symbols by length, by symbol order within each length.
        for (symbol = 0; symbol < codeLengthsSize; ++symbol)
        {
            int symbolCodeLength = codeLengths[symbol];
            if (symbolCodeLength > 0)
            {
                sorted[offsets[symbolCodeLength]++] = symbol;
            }
        }

        // Special case code with only one value.
        if (offsets[MaximumCodeLength] == 1)
        {
            HuffmanCode huffmanCode = new()
            {
                BitsUsed = 0,
                Value = (uint)sorted[0]
            };
            if (!table.IsEmpty)
            {
                ReplicateValue(table, 1, totalSize, huffmanCode);
            }

            return totalSize;
        }

        int step; // step size to replicate values in current table
        int low = -1;     // low bits for current root entry
        int mask = totalSize - 1;    // mask for low bits
        int key = 0;      // reversed prefix code
        int numOpen = 1;      // number of open branches in current tree level
        int tableBits = rootBits;        // key length of current table
        int tableSize = 1 << tableBits;  // size of current table
        symbol = 0;

        // Fill in root table.
        for (len = 1, step = 2; len <= rootBits; ++len, step <<= 1)
        {
            int countsLen = counts[len];
            numOpen <<= 1;
            numOpen -= counts[len];
            if (numOpen < 0)
            {
                return 0;
            }

            for (; countsLen > 0; countsLen--)
            {
                HuffmanCode huffmanCode = new()
                {
                    BitsUsed = len,
                    Value = (uint)sorted[symbol++]
                };
                if (!table.IsEmpty)
                {
                    ReplicateValue(table[key..], step, tableSize, huffmanCode);
                }

                key = GetNextKey(key, len);
            }

            counts[len] = countsLen;
        }

        // Fill in 2nd level tables and add pointers to root table.
        Span<HuffmanCode> tableSpan = table;
        int tablePos = 0;
        for (len = rootBits + 1, step = 2; len <= MaximumCodeLength; ++len, step <<= 1)
        {
            numOpen <<= 1;
            numOpen -= counts[len];
            if (numOpen < 0)
            {
                return 0;
            }

            for (; counts[len] > 0; --counts[len])
            {
                if ((key & mask) != low)
                {
                    if (!table.IsEmpty)
                    {
                        tableSpan = tableSpan[tableSize..];
                    }

                    tablePos += tableSize;
                    tableBits = NextTableBitSize(counts, len, rootBits);
                    tableSize = 1 << tableBits;
                    totalSize += tableSize;
                    low = key & mask;
                    if (!table.IsEmpty)
                    {
                        table[low] = new HuffmanCode
                        {
                            BitsUsed = tableBits + rootBits,
                            Value = (uint)(tablePos - low)
                        };
                    }
                }

                HuffmanCode huffmanCode = new()
                {
                    BitsUsed = len - rootBits,
                    Value = (uint)sorted[symbol++]
                };
                if (!table.IsEmpty)
                {
                    ReplicateValue(tableSpan[(key >> rootBits)..], step, tableSize, huffmanCode);
                }

                key = GetNextKey(key, len);
            }
        }

        return totalSize;
    }

    /// <summary>
    /// Returns the table width of the next 2nd level table. count is the histogram of bit lengths for the remaining symbols,
    /// len is the code length of the next processed symbol.
    /// </summary>
    private static int NextTableBitSize(ReadOnlySpan<int> count, int len, int rootBits)
    {
        int left = 1 << (len - rootBits);
        while (len < MaximumCodeLength)
        {
            left -= count[len];
            if (left <= 0)
            {
                break;
            }

            ++len;
            left <<= 1;
        }

        return len - rootBits;
    }

    /// <summary>
    /// Stores code in table[0], table[step], table[2*step], ..., table[end-step].
    /// Assumes that end is an integer multiple of step.
    /// </summary>
    private static void ReplicateValue(Span<HuffmanCode> table, int step, int end, HuffmanCode code)
    {
        DebugGuard.IsTrue(end % step == 0, nameof(end), "end must be a multiple of step");

        do
        {
            end -= step;
            table[end] = code;
        }
        while (end > 0);
    }

    /// <summary>
    /// Returns reverse(reverse(key, len) + 1, len), where reverse(key, len) is the
    /// bit-wise reversal of the len least significant bits of key.
    /// </summary>
    private static int GetNextKey(int key, int len)
    {
        int step = 1 << (len - 1);
        while ((key & step) != 0)
        {
            step >>= 1;
        }

        return step != 0 ? (key & (step - 1)) + step : key;
    }
}
