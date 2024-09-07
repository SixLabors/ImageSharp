// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

/// <summary>
/// Utility functions related to creating the huffman tables.
/// </summary>
internal static class HuffmanUtils
{
    public const int HuffmanTableBits = 8;

    public const int HuffmanPackedBits = 6;

    public const int HuffmanTableMask = (1 << HuffmanTableBits) - 1;

    public const uint HuffmanPackedTableSize = 1u << HuffmanPackedBits;

    // Pre-reversed 4-bit values.
    private static readonly byte[] ReversedBits =
    {
        0x0, 0x8, 0x4, 0xc, 0x2, 0xa, 0x6, 0xe,
        0x1, 0x9, 0x5, 0xd, 0x3, 0xb, 0x7, 0xf
    };

    public static void CreateHuffmanTree(Span<uint> histogram, int treeDepthLimit, bool[] bufRle, Span<HuffmanTree> huffTree, HuffmanTreeCode huffCode)
    {
        int numSymbols = huffCode.NumSymbols;
        bufRle.AsSpan().Clear();
        OptimizeHuffmanForRle(numSymbols, bufRle, histogram);
        GenerateOptimalTree(huffTree, histogram, numSymbols, treeDepthLimit, huffCode.CodeLengths);

        // Create the actual bit codes for the bit lengths.
        ConvertBitDepthsToSymbols(huffCode);
    }

    /// <summary>
    /// Change the population counts in a way that the consequent
    /// Huffman tree compression, especially its RLE-part, give smaller output.
    /// </summary>
    public static void OptimizeHuffmanForRle(int length, bool[] goodForRle, Span<uint> counts)
    {
        // 1) Let's make the Huffman code more compatible with rle encoding.
        for (; length >= 0; --length)
        {
            if (length == 0)
            {
                return;  // All zeros.
            }

            if (counts[length - 1] != 0)
            {
                // Now counts[0..length - 1] does not have trailing zeros.
                break;
            }
        }

        // 2) Let's mark all population counts that already can be encoded with an rle code.
        // Let's not spoil any of the existing good rle codes.
        // Mark any seq of 0's that is longer as 5 as a goodForRle.
        // Mark any seq of non-0's that is longer as 7 as a goodForRle.
        uint symbol = counts[0];
        int stride = 0;
        for (int i = 0; i < length + 1; i++)
        {
            if (i == length || counts[i] != symbol)
            {
                if ((symbol == 0 && stride >= 5) || (symbol != 0 && stride >= 7))
                {
                    for (int k = 0; k < stride; k++)
                    {
                        goodForRle[i - k - 1] = true;
                    }
                }

                stride = 1;
                if (i != length)
                {
                    symbol = counts[i];
                }
            }
            else
            {
                ++stride;
            }
        }

        // 3) Let's replace those population counts that lead to more rle codes.
        stride = 0;
        uint limit = counts[0];
        uint sum = 0;
        for (int i = 0; i < length + 1; i++)
        {
            if (i == length || goodForRle[i] || (i != 0 && goodForRle[i - 1]) || !ValuesShouldBeCollapsedToStrideAverage((int)counts[i], (int)limit))
            {
                if (stride >= 4 || (stride >= 3 && sum == 0))
                {
                    uint k;

                    // The stride must end, collapse what we have, if we have enough (4).
                    uint count = (sum + ((uint)stride / 2)) / (uint)stride;
                    if (count < 1)
                    {
                        count = 1;
                    }

                    if (sum == 0)
                    {
                        // Don't make an all zeros stride to be upgraded to ones.
                        count = 0;
                    }

                    for (k = 0; k < stride; k++)
                    {
                        // We don't want to change value at counts[i],
                        // that is already belonging to the next stride. Thus - 1.
                        counts[(int)(i - k - 1)] = count;
                    }
                }

                stride = 0;
                sum = 0;
                if (i < length - 3)
                {
                    // All interesting strides have a count of at least 4, at least when non-zeros.
                    limit = (counts[i] + counts[i + 1] +
                             counts[i + 2] + counts[i + 3] + 2) / 4;
                }
                else if (i < length)
                {
                    limit = counts[i];
                }
                else
                {
                    limit = 0;
                }
            }

            ++stride;
            if (i != length)
            {
                sum += counts[i];
                if (stride >= 4)
                {
                    limit = (sum + ((uint)stride / 2)) / (uint)stride;
                }
            }
        }
    }

    /// <summary>
    /// Create an optimal Huffman tree.
    /// </summary>
    /// <see href="http://en.wikipedia.org/wiki/Huffman_coding"/>
    /// <param name="tree">The huffman tree.</param>
    /// <param name="histogram">The histogram.</param>
    /// <param name="histogramSize">The size of the histogram.</param>
    /// <param name="treeDepthLimit">The tree depth limit.</param>
    /// <param name="bitDepths">How many bits are used for the symbol.</param>
    public static void GenerateOptimalTree(Span<HuffmanTree> tree, Span<uint> histogram, int histogramSize, int treeDepthLimit, byte[] bitDepths)
    {
        uint countMin;
        int treeSizeOrig = 0;

        for (int i = 0; i < histogramSize; i++)
        {
            if (histogram[i] != 0)
            {
                ++treeSizeOrig;
            }
        }

        if (treeSizeOrig == 0)
        {
            return;
        }

        Span<HuffmanTree> treePool = tree[treeSizeOrig..];

        // For block sizes with less than 64k symbols we never need to do a
        // second iteration of this loop.
        for (countMin = 1; ; countMin *= 2)
        {
            int treeSize = treeSizeOrig;

            // We need to pack the Huffman tree in treeDepthLimit bits.
            // So, we try by faking histogram entries to be at least 'countMin'.
            int idx = 0;
            for (int j = 0; j < histogramSize; j++)
            {
                if (histogram[j] != 0)
                {
                    uint count = histogram[j] < countMin ? countMin : histogram[j];
                    tree[idx].TotalCount = (int)count;
                    tree[idx].Value = j;
                    tree[idx].PoolIndexLeft = -1;
                    tree[idx].PoolIndexRight = -1;
                    idx++;
                }
            }

            // Build the Huffman tree.
            Span<HuffmanTree> treeSlice = tree[..treeSize];
            treeSlice.Sort(HuffmanTree.Compare);

            if (treeSize > 1)
            {
                // Normal case.
                int treePoolSize = 0;
                while (treeSize > 1)
                {
                    // Finish when we have only one root.
                    treePool[treePoolSize++] = tree[treeSize - 1];
                    treePool[treePoolSize++] = tree[treeSize - 2];
                    int count = treePool[treePoolSize - 1].TotalCount + treePool[treePoolSize - 2].TotalCount;
                    treeSize -= 2;

                    // Search for the insertion point.
                    int k;
                    for (k = 0; k < treeSize; k++)
                    {
                        if (tree[k].TotalCount <= count)
                        {
                            break;
                        }
                    }

                    int endIdx = k + 1;
                    int num = treeSize - k;
                    int startIdx = endIdx + num - 1;
                    for (int i = startIdx; i >= endIdx; i--)
                    {
                        tree[i] = tree[i - 1];
                    }

                    tree[k].TotalCount = count;
                    tree[k].Value = -1;
                    tree[k].PoolIndexLeft = treePoolSize - 1;
                    tree[k].PoolIndexRight = treePoolSize - 2;
                    treeSize++;
                }

                SetBitDepths(tree, treePool, bitDepths, 0);
            }
            else if (treeSize == 1)
            {
                // Trivial case: only one element.
                bitDepths[tree[0].Value] = 1;
            }

            // Test if this Huffman tree satisfies our 'treeDepthLimit' criteria.
            int maxDepth = bitDepths[0];
            for (int j = 1; j < histogramSize; j++)
            {
                if (maxDepth < bitDepths[j])
                {
                    maxDepth = bitDepths[j];
                }
            }

            if (maxDepth <= treeDepthLimit)
            {
                break;
            }
        }
    }

    public static int CreateCompressedHuffmanTree(HuffmanTreeCode tree, HuffmanTreeToken[] tokensArray)
    {
        int depthSize = tree.NumSymbols;
        int prevValue = 8;  // 8 is the initial value for rle.
        int i = 0;
        int tokenPos = 0;
        while (i < depthSize)
        {
            int value = tree.CodeLengths[i];
            int k = i + 1;
            while (k < depthSize && tree.CodeLengths[k] == value)
            {
                k++;
            }

            int runs = k - i;
            if (value == 0)
            {
                tokenPos += CodeRepeatedZeros(runs, tokensArray.AsSpan(tokenPos));
            }
            else
            {
                tokenPos += CodeRepeatedValues(runs, tokensArray.AsSpan(tokenPos), value, prevValue);
                prevValue = value;
            }

            i += runs;
        }

        return tokenPos;
    }

    public static int BuildHuffmanTable(Span<HuffmanCode> table, int rootBits, int[] codeLengths, int codeLengthsSize)
    {
        DebugGuard.MustBeGreaterThan(rootBits, 0, nameof(rootBits));
        DebugGuard.NotNull(codeLengths, nameof(codeLengths));
        DebugGuard.MustBeGreaterThan(codeLengthsSize, 0, nameof(codeLengthsSize));

        // sorted[codeLengthsSize] is a pre-allocated array for sorting symbols by code length.
        Span<int> sorted = codeLengthsSize <= 64 ? stackalloc int[codeLengthsSize] : new int[codeLengthsSize];
        int totalSize = 1 << rootBits; // total size root table + 2nd level table.
        int len; // current code length.
        int symbol; // symbol index in original or sorted table.
        Span<int> counts = stackalloc int[WebpConstants.MaxAllowedCodeLength + 1]; // number of codes of each length.
        Span<int> offsets = stackalloc int[WebpConstants.MaxAllowedCodeLength + 1]; // offsets in sorted table for each length.

        // Build histogram of code lengths.
        for (symbol = 0; symbol < codeLengthsSize; ++symbol)
        {
            int codeLengthOfSymbol = codeLengths[symbol];
            if (codeLengthOfSymbol > WebpConstants.MaxAllowedCodeLength)
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
        for (len = 1; len < WebpConstants.MaxAllowedCodeLength; ++len)
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
        if (offsets[WebpConstants.MaxAllowedCodeLength] == 1)
        {
            HuffmanCode huffmanCode = new()
            {
                BitsUsed = 0,
                Value = (uint)sorted[0]
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
            int countsLen = counts[len];
            numOpen <<= 1;
            numNodes += numOpen;
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
                ReplicateValue(table[key..], step, tableSize, huffmanCode);
                key = GetNextKey(key, len);
            }

            counts[len] = countsLen;
        }

        // Fill in 2nd level tables and add pointers to root table.
        Span<HuffmanCode> tableSpan = table;
        int tablePos = 0;
        for (len = rootBits + 1, step = 2; len <= WebpConstants.MaxAllowedCodeLength; ++len, step <<= 1)
        {
            numOpen <<= 1;
            numNodes += numOpen;
            numOpen -= counts[len];
            if (numOpen < 0)
            {
                return 0;
            }

            for (; counts[len] > 0; --counts[len])
            {
                if ((key & mask) != low)
                {
                    tableSpan = tableSpan[tableSize..];
                    tablePos += tableSize;
                    tableBits = NextTableBitSize(counts, len, rootBits);
                    tableSize = 1 << tableBits;
                    totalSize += tableSize;
                    low = key & mask;
                    table[low] = new()
                    {
                        BitsUsed = tableBits + rootBits,
                        Value = (uint)(tablePos - low)
                    };
                }

                HuffmanCode huffmanCode = new()
                {
                    BitsUsed = len - rootBits,
                    Value = (uint)sorted[symbol++]
                };
                ReplicateValue(tableSpan[(key >> rootBits)..], step, tableSize, huffmanCode);
                key = GetNextKey(key, len);
            }
        }

        return totalSize;
    }

    private static int CodeRepeatedZeros(int repetitions, Span<HuffmanTreeToken> tokens)
    {
        int pos = 0;
        while (repetitions >= 1)
        {
            if (repetitions < 3)
            {
                for (int i = 0; i < repetitions; i++)
                {
                    tokens[pos].Code = 0;   // 0-value
                    tokens[pos].ExtraBits = 0;
                    pos++;
                }

                break;
            }

            if (repetitions < 11)
            {
                tokens[pos].Code = 17;
                tokens[pos].ExtraBits = (byte)(repetitions - 3);
                pos++;
                break;
            }

            if (repetitions < 139)
            {
                tokens[pos].Code = 18;
                tokens[pos].ExtraBits = (byte)(repetitions - 11);
                pos++;
                break;
            }

            tokens[pos].Code = 18;
            tokens[pos].ExtraBits = 0x7f;  // 138 repeated 0s
            pos++;
            repetitions -= 138;
        }

        return pos;
    }

    private static int CodeRepeatedValues(int repetitions, Span<HuffmanTreeToken> tokens, int value, int prevValue)
    {
        int pos = 0;

        if (value != prevValue)
        {
            tokens[pos].Code = (byte)value;
            tokens[pos].ExtraBits = 0;
            pos++;
            repetitions--;
        }

        while (repetitions >= 1)
        {
            if (repetitions < 3)
            {
                int i;
                for (i = 0; i < repetitions; i++)
                {
                    tokens[pos].Code = (byte)value;
                    tokens[pos].ExtraBits = 0;
                    pos++;
                }

                break;
            }

            if (repetitions < 7)
            {
                tokens[pos].Code = 16;
                tokens[pos].ExtraBits = (byte)(repetitions - 3);
                pos++;
                break;
            }

            tokens[pos].Code = 16;
            tokens[pos].ExtraBits = 3;
            pos++;
            repetitions -= 6;
        }

        return pos;
    }

    /// <summary>
    /// Get the actual bit values for a tree of bit depths.
    /// </summary>
    /// <param name="tree">The huffman tree.</param>
    private static void ConvertBitDepthsToSymbols(HuffmanTreeCode tree)
    {
        // 0 bit-depth means that the symbol does not exist.
        Span<uint> nextCode = stackalloc uint[WebpConstants.MaxAllowedCodeLength + 1];
        Span<int> depthCount = stackalloc int[WebpConstants.MaxAllowedCodeLength + 1];

        int len = tree.NumSymbols;
        for (int i = 0; i < len; i++)
        {
            int codeLength = tree.CodeLengths[i];
            depthCount[codeLength]++;
        }

        depthCount[0] = 0;  // ignore unused symbol.
        nextCode[0] = 0;

        uint code = 0;
        for (int i = 1; i <= WebpConstants.MaxAllowedCodeLength; i++)
        {
            code = (uint)((code + depthCount[i - 1]) << 1);
            nextCode[i] = code;
        }

        for (int i = 0; i < len; i++)
        {
            int codeLength = tree.CodeLengths[i];
            tree.Codes[i] = (short)ReverseBits(codeLength, nextCode[codeLength]++);
        }
    }

    private static void SetBitDepths(Span<HuffmanTree> tree, Span<HuffmanTree> pool, byte[] bitDepths, int level)
    {
        if (tree[0].PoolIndexLeft >= 0)
        {
            SetBitDepths(pool[tree[0].PoolIndexLeft..], pool, bitDepths, level + 1);
            SetBitDepths(pool[tree[0].PoolIndexRight..], pool, bitDepths, level + 1);
        }
        else
        {
            bitDepths[tree[0].Value] = (byte)level;
        }
    }

    private static uint ReverseBits(int numBits, uint bits)
    {
        uint retval = 0;
        int i = 0;
        while (i < numBits)
        {
            i += 4;
            retval |= (uint)(ReversedBits[bits & 0xf] << (WebpConstants.MaxAllowedCodeLength + 1 - i));
            bits >>= 4;
        }

        retval >>= WebpConstants.MaxAllowedCodeLength + 1 - numBits;
        return retval;
    }

    /// <summary>
    /// Returns the table width of the next 2nd level table. count is the histogram of bit lengths for the remaining symbols,
    /// len is the code length of the next processed symbol.
    /// </summary>
    private static int NextTableBitSize(ReadOnlySpan<int> count, int len, int rootBits)
    {
        int left = 1 << (len - rootBits);
        while (len < WebpConstants.MaxAllowedCodeLength)
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

    /// <summary>
    /// Heuristics for selecting the stride ranges to collapse.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static bool ValuesShouldBeCollapsedToStrideAverage(int a, int b) => Math.Abs(a - b) < 4;
}
