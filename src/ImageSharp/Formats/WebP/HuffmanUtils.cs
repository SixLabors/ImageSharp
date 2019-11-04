// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal static class HuffmanUtils
    {
        public static int BuildHuffmanTable(HuffmanCode[] table, int rootBits, int[] codeLengths, int codeLengthsSize)
        {
            Guard.MustBeGreaterThan(rootBits, 0, nameof(rootBits));
            Guard.NotNull(codeLengths, nameof(codeLengths));
            Guard.MustBeGreaterThan(codeLengthsSize, 0, nameof(codeLengthsSize));

            // sorted[code_lengths_size] is a pre-allocated array for sorting symbols by code length.
            var sorted = new int[codeLengthsSize];
            // total size root table + 2nd level table
            int totalSize = 1 << rootBits;
            // current code length
            int len;
            // symbol index in original or sorted table
            int symbol;
            // number of codes of each length:
            var count = new int[WebPConstants.MaxAllowedCodeLength + 1];
            // offsets in sorted table for each length
            var offset = new int[WebPConstants.MaxAllowedCodeLength + 1];

            // Build histogram of code lengths.
            for (symbol = 0; symbol < codeLengthsSize; ++symbol)
            {
                if (codeLengths[symbol] > WebPConstants.MaxAllowedCodeLength)
                {
                    return 0;
                }

                ++count[codeLengths[symbol]];
            }

            // Generate offsets into sorted symbol table by code length.
            offset[1] = 0;
            for (len = 1; len < WebPConstants.MaxAllowedCodeLength; ++len)
            {
                if (count[len] > (1 << len))
                {
                    return 0;
                }

                offset[len + 1] = offset[len] + count[len];
            }

            // Sort symbols by length, by symbol order within each length.
            for (symbol = 0; symbol < codeLengthsSize; ++symbol)
            {
                int symbolCodeLength = codeLengths[symbol];
                if (codeLengths[symbol] > 0)
                {
                    sorted[offset[symbolCodeLength]++] = symbol;
                }
            }

            // Special case code with only one value.
            if (offset[WebPConstants.MaxAllowedCodeLength] is 1)
            {
                var huffmanCode = new HuffmanCode()
                {
                    BitsUsed = 0,
                    Value = sorted[0]
                };
                ReplicateValue(table, 1, totalSize, huffmanCode);
                return totalSize;
            }

            int step; // step size to replicate values in current table
            int low = -1;     // low bits for current root entry
            int mask = totalSize - 1;    // mask for low bits
            int key = 0;      // reversed prefix code
            int numNodes = 1;     // number of Huffman tree nodes
            int numOpen = 1;      // number of open branches in current tree level
            int tableBits = rootBits;        // key length of current table
            int tableSize = 1 << tableBits;  // size of current table
            symbol = 0;
            // Fill in root table.
            for (len = 1, step = 2; len <= rootBits; ++len, step <<= 1)
            {
                numOpen <<= 1;
                numNodes += numOpen;
                numOpen -= count[len];
                if (numOpen < 0)
                {
                    return 0;
                }

                for (; count[len] > 0; --count[len])
                {
                    var huffmanCode = new HuffmanCode()
                    {
                        BitsUsed = len,
                        Value = sorted[symbol++]
                    };
                    ReplicateValue(table.AsSpan(key), step, tableSize, huffmanCode);
                    key = GetNextKey(key, len);
                }
            }

            // Fill in 2nd level tables and add pointers to root table.
            for (len = rootBits + 1, step = 2; len <= WebPConstants.MaxAllowedCodeLength; ++len, step <<= 1)
            {
                numOpen <<= 1;
                numNodes += numOpen;
                numOpen -= count[len];
                if (numOpen < 0)
                {
                    return 0;
                }

                Span<HuffmanCode> tableSpan = table.AsSpan();
                for (; count[len] > 0; --count[len])
                {
                    if ((key & mask) != low)
                    {
                        tableSpan = tableSpan.Slice(tableSize);
                        tableBits = NextTableBitSize(count, len, rootBits);
                        tableSize = 1 << tableBits;
                        totalSize += tableSize;
                        low = key & mask;
                        // TODO: fix this
                        //rootTable[low].bits = (tableBits + rootBits);
                        //rootTable[low].value = ((table - rootTable) - low);
                    }

                    var huffmanCode = new HuffmanCode
                    {
                        BitsUsed = len - rootBits,
                        Value = sorted[symbol++]
                    };
                    ReplicateValue(tableSpan.Slice(key >> rootBits), step, tableSize, huffmanCode);
                    key = GetNextKey(key, len);
                }
            }

            return totalSize;
        }

        /// <summary>
        /// Returns the table width of the next 2nd level table. count is the histogram of bit lengths for the remaining symbols,
        /// len is the code length of the next processed symbol.
        /// </summary>
        private static int NextTableBitSize(int[] count, int len, int rootBits)
        {
            int left = 1 << (len - rootBits);
            while (len < WebPConstants.MaxAllowedCodeLength)
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
        /// Stores code in table[0], table[step], table[2*step], ..., table[end].
        /// Assumes that end is an integer multiple of step.
        /// </summary>
        private static void ReplicateValue(Span<HuffmanCode> table, int step, int end, HuffmanCode code)
        {
            Guard.IsTrue(end % step == 0, nameof(end), "end must be a multiple of step");

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
}
