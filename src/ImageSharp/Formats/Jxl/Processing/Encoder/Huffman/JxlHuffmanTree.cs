// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Huffman;

/// <summary>
/// Node of a Huffman tree.
/// </summary>
internal struct JxlHuffmanTree(int count, short left, short right)
{
    public int TotalCount = count;

    /// <summary>
    /// Index of the left node of the tree.
    /// </summary>
    public short IndexLeft = left;

    /// <summary>
    /// Index of the right node of the tree. If it's missing
    /// then this is the value of the node.
    /// </summary>
    public short IndexRightOrValue = right;

    /// <summary>
    /// Gets a lookup table with pre-reversed 4-bit values.
    /// This lookup is used by <see cref="ReverseBits(int, short)"/>.
    /// </summary>
    private static ReadOnlySpan<int> ReverseLookup =>
    [
        0x0, 0x8, 0x4, 0xc, 0x2, 0xa, 0x6, 0xe,
        0x1, 0x9, 0x5, 0xd, 0x3, 0xb, 0x7, 0xf
    ];

    public static void SetDepth(ref JxlHuffmanTree p, Span<JxlHuffmanTree> pool, Span<byte> depth, byte level)
    {
        if (p.IndexLeft >= 0)
        {
            level++;
            SetDepth(ref pool[p.IndexLeft], pool, depth, level);
            SetDepth(ref pool[p.IndexRightOrValue], pool, depth, level);
        }
        else
        {
            depth[p.IndexRightOrValue] = level;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Compare(JxlHuffmanTree v0, JxlHuffmanTree v1)
    {
        if (v0.TotalCount != v1.TotalCount)
        {
            return v0.TotalCount.CompareTo(v1.TotalCount);
        }

        return v0.IndexRightOrValue.CompareTo(v1.IndexRightOrValue);
    }

    public static void CreateHuffmanTree(Span<int> data, int length, int treeLimit, Span<byte> depth)
    {
        JxlHuffmanTree[]? pool = null;

        // This is basically the equivalent of List<T> but is fixed-size.
        // We don't need an entire collection on the heap.
        int desiredTreeItems = (2 * length) + 1;
        Span<JxlHuffmanTree> tree =
            desiredTreeItems <= 256
            ? stackalloc JxlHuffmanTree[256].Slice(0, desiredTreeItems)
            : pool = ArrayPool<JxlHuffmanTree>.Shared.Rent(desiredTreeItems);

        // Number of items in our "fixed List<T>". So in other to
        // add to the tree we do 'tree[treeRef++] = ...'.
        int treeRef = 0;

        for (int countLimit = 1; ; countLimit *= 2)
        {
            tree.Clear(); // Always clear on every iteration

            int i = length;
            for (; i != 0;)
            {
                --i;
                if (data[i] != 0)
                {
                    int count = Math.Max(data[i], countLimit - 1);
                    tree[treeRef++] = new JxlHuffmanTree(count, -1, (short)i);
                }
            }

            if (treeRef == 1)
            {
                // Fake value; will be fixed on upper level.
                depth[tree[0].IndexRightOrValue] = 1;
                break;
            }

            tree.Sort(Compare);

            JxlHuffmanTree sentinel = new(int.MaxValue, -1, -1);
            tree[treeRef++] = sentinel;
            tree[treeRef++] = sentinel; // We do this twice, yes

            i = 0;
            int j = treeRef + 1;

            for (int k = treeRef - 1; k != 0; --k)
            {
                int left;
                int right;

                if (tree[i].TotalCount <= tree[j].TotalCount)
                {
                    left = i;
                    i++;
                }
                else
                {
                    left = j;
                    j++;
                }

                if (tree[i].TotalCount <= tree[j].TotalCount)
                {
                    right = i;
                    i++;
                }
                else
                {
                    right = j;
                    j++;
                }

                int j_end = treeRef - 1;
                ref JxlHuffmanTree currTree = ref tree[j_end];
                currTree.TotalCount = tree[left].TotalCount + tree[right].TotalCount;
                currTree.IndexLeft = (short)left;
                currTree.IndexRightOrValue = (short)right;

                tree[treeRef++] = sentinel;
            }

            SetDepth(ref tree[(2 * treeRef) - 1], tree, depth, 0);

            if (TensorPrimitives.Max((ReadOnlySpan<byte>)depth[..length]) <= treeLimit)
            {
                break;
            }
        }

        // Don't forget to return the pooled array
        if (pool is not null)
        {
            ArrayPool<JxlHuffmanTree>.Shared.Return(pool);
        }
    }

    public static void Reverse(Span<byte> v, int start, int end)
    {
        end--;
        while (start < end)
        {
            RuntimeUtility.Swap(ref v[end], ref v[start]);
            start++;
            end++;
        }
    }

    public static void WriteHuffmanTreeRepetitions(byte previousValue, byte value, int repetitions, ref int treeSize, Span<byte> tree, Span<byte> extraBitsData)
    {
        DebugGuard.MustBeGreaterThan(repetitions, 0, nameof(repetitions));

        if (previousValue != value)
        {
            tree[treeSize] = value;
            extraBitsData[treeSize] = 0;
            treeSize++;
            repetitions--;
        }

        if (repetitions == 7)
        {
            tree[treeSize] = value;
            extraBitsData[treeSize] = 0;
            treeSize++;
        }

        if (repetitions < 3)
        {
            for (int i = 0; i < repetitions; ++i)
            {
                tree[treeSize] = value;
                extraBitsData[treeSize] = 0;
                treeSize++;
            }
        }
        else
        {
            repetitions -= 3;
            int start = treeSize;
            while (true)
            {
                tree[treeSize] = 16;
                extraBitsData[treeSize] = (byte)(repetitions & 0x3);
                treeSize++;
                repetitions >>= 2;

                if (repetitions == 0)
                {
                    break;
                }

                repetitions--;
            }

            Reverse(tree, start, treeSize);
            Reverse(extraBitsData, start, treeSize);
        }
    }

    public static void WriteHuffmanTreeRepetitionsZeros(int repetitions, ref int treeSize, Span<byte> tree, Span<byte> extraBitsData)
    {
        if (repetitions == 11)
        {
            tree[treeSize] = 0;
            extraBitsData[treeSize] = 0;
            treeSize++;
            repetitions--;
        }

        if (repetitions < 3)
        {
            for (int i = 0; i < repetitions; ++i)
            {
                tree[treeSize] = 0;
                extraBitsData[treeSize] = 0;
                treeSize++;
            }
        }
        else
        {
            repetitions -= 3;
            int start = treeSize;

            while (true)
            {
                tree[treeSize] = 17;
                extraBitsData[treeSize] = (byte)(repetitions & 0x7);
                treeSize++;
                repetitions >>= 3;

                if (repetitions == 0)
                {
                    break;
                }

                repetitions--;
            }

            Reverse(tree, start, treeSize);
            Reverse(extraBitsData, start, treeSize);
        }
    }

    // Decides whether or not to use Run Length Encoding (RLE).
    // Basically that's where, for example, when we have a
    // string of repetitive letters "aaaaaa", instead of encoding
    // them all separately, it encodes "a times 6".
    public static void DecideOverRleUse(Span<byte> depth, int length, ref bool useRleForNonZero, ref bool useRleForZero)
    {
        int totalRepsZero = 0;
        int totalRepsNonZero = 0;
        int countRepsZero = 1;
        int countRepsNonZero = 1;

        for (int i = 0; i < length;)
        {
            byte value = depth[i];
            int reps = 1;

            for (int k = i + 1; k < length && depth[k] == value; k++)
            {
                reps++;
            }

            if (reps >= 3 && value == 0)
            {
                totalRepsZero += reps;
                countRepsZero++;
            }

            if (reps >= 4 && value != 0)
            {
                totalRepsNonZero += reps;
                countRepsNonZero++;
            }

            i += reps;
        }

        useRleForNonZero = totalRepsNonZero > countRepsNonZero * 2;
        useRleForZero = totalRepsZero > countRepsZero * 2;
    }

    public static void WriteHuffmanTree(Span<byte> depth, int length, ref int treeSize, Span<byte> tree, Span<byte> extraBitsData)
    {
        byte previousValue = 8;
        int newLength = length;

        for (int i = 0; i < length; i++)
        {
            if (depth[length - i - 1] == 0)
            {
                newLength--;
            }
            else
            {
                break;
            }
        }

        bool useRleForNonZeroes = false;
        bool useRleForZero = false;

        if (length > 50)
        {
            DecideOverRleUse(depth, newLength, ref useRleForNonZeroes, ref useRleForZero);
        }

        for (int i = 0; i < newLength;)
        {
            byte value = depth[i];
            int reps = 1;

            if ((value != 0 && useRleForNonZeroes) || (value == 0 && useRleForZero))
            {
                for (int k = i + 1; k < newLength && depth[k] == value; k++)
                {
                    reps++;
                }
            }

            if (value == 0)
            {
                WriteHuffmanTreeRepetitionsZeros(reps, ref treeSize, tree, extraBitsData);
            }
            else
            {
                WriteHuffmanTreeRepetitions(previousValue, value, reps, ref treeSize, tree, extraBitsData);
                previousValue = value;
            }

            i += reps;
        }
    }

    public static short ReverseBits(int numBits, short bits)
    {
        int result = ReverseLookup[bits & 0xf];

        for (int i = 4; i < numBits; i += 4)
        {
            result <<= 4;
            bits = (short)(bits >> 4);
            result |= ReverseLookup[bits & 0xf];
        }

        result >>= -numBits & 0x3;

        return (short)result;
    }

    public static void ConvertBitDepthsToSymbols(Span<byte> depth, int len, Span<short> bits)
    {
        // In Brotli, all bit depths are [1..15]
        // 0 bit depth means that the symbol does not exist.
        const int maxBits = 16; // 0..15 are values for bits

        Span<short> blCount = stackalloc short[maxBits];
        blCount.Clear(); // explicitly cleared from reference

        for (int i = 0; i < len; i++)
        {
            blCount[depth[i]]++;
        }

        blCount[0] = 0;

        Span<short> nextCode = stackalloc short[maxBits]; // not cleared in reference
        nextCode[0] = 0;

        int code = 0;
        for (int i = 1; i < maxBits; ++i)
        {
            code = (code + blCount[i - 1]) << 1;
            nextCode[i] = (short)code;
        }

        for (int i = 0; i < len; ++i)
        {
            if (depth[i] != 0)
            {
                bits[i] = ReverseBits(depth[i], nextCode[depth[i]]++);
            }
        }
    }
}
