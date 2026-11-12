// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Compression;

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
    [
        0x0, 0x8, 0x4, 0xc, 0x2, 0xa, 0x6, 0xe,
        0x1, 0x9, 0x5, 0xd, 0x3, 0xb, 0x7, 0xf
    ];

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
    public static void GenerateOptimalTree(Span<HuffmanTree> tree, ReadOnlySpan<uint> histogram, int histogramSize, int treeDepthLimit, byte[] bitDepths)
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

    /// <summary>
    /// Builds the lossless WebP Huffman table through the shared canonical table builder.
    /// </summary>
    /// <param name="table">The lookup storage.</param>
    /// <param name="rootBits">The first lookup width.</param>
    /// <param name="codeLengths">The code lengths for the alphabet.</param>
    /// <param name="codeLengthsSize">The number of alphabet symbols.</param>
    /// <returns>The table entry count, or zero for an invalid code.</returns>
    public static int BuildHuffmanTable(Span<HuffmanCode> table, int rootBits, int[] codeLengths, int codeLengthsSize)
    {
        Span<int> sorted = codeLengthsSize <= 64 ? stackalloc int[codeLengthsSize] : new int[codeLengthsSize];
        return HuffmanTableBuilder.Build(table, rootBits, codeLengths.AsSpan(0, codeLengthsSize), sorted);
    }

    private static int CodeRepeatedZeros(int repetitions, ReadOnlySpan<HuffmanTreeToken> tokens)
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

    private static int CodeRepeatedValues(int repetitions, ReadOnlySpan<HuffmanTreeToken> tokens, int value, int prevValue)
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

    private static void SetBitDepths(ReadOnlySpan<HuffmanTree> tree, Span<HuffmanTree> pool, byte[] bitDepths, int level)
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
    /// Heuristics for selecting the stride ranges to collapse.
    /// </summary>
    [MethodImpl(InliningOptions.ShortMethod)]
    private static bool ValuesShouldBeCollapsedToStrideAverage(int a, int b) => Math.Abs(a - b) < 4;
}
