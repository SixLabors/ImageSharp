// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Huffman;

/// <summary>
/// Derives &amp; writes Huffman codes.
/// </summary>
internal static class JxlHuffmanEncoder
{
    private const int CodeLengthCodes = 18;

    private static ReadOnlySpan<byte> StorageOrder => [1, 2, 3, 4, 0, 5, 17, 6, 16, 7, 8, 9, 10, 11, 12, 13, 14, 15];

    private static ReadOnlySpan<byte> HuffmanBitLengthHuffmanCodeSymbols => [0, 7, 3, 2, 1, 15];

    private static ReadOnlySpan<byte> HuffmanBitLengthHuffmanCodeBitLengths => [2, 4, 3, 2, 2, 4];

    public static void StoreHuffmanTreeOfHuffmanTreeToBitMask(int numCodes, Span<byte> codeLengthBitDepth, JxlBitWriter writer)
    {
        int codesToStore = CodeLengthCodes;
        if (numCodes > 1)
        {
            for (; codesToStore > 0; codesToStore--)
            {
                if (codeLengthBitDepth[StorageOrder[codesToStore - 1]] != 0)
                {
                    break;
                }
            }
        }

        int skipSome = 0;
        if (codeLengthBitDepth[StorageOrder[0]] == 0 && codeLengthBitDepth[StorageOrder[1]] == 0)
        {
            skipSome = 2; // skips two
            if (codeLengthBitDepth[StorageOrder[2]] == 0)
            {
                skipSome = 3; // skips three
            }
        }

        writer.Write(2, skipSome);

        for (int i = skipSome; i < codesToStore; ++i)
        {
            int l = codeLengthBitDepth[StorageOrder[i]];
            writer.Write(HuffmanBitLengthHuffmanCodeBitLengths[l], HuffmanBitLengthHuffmanCodeSymbols[l]);
        }
    }

    public static void StoreHuffmanTreeToBitMask(int huffmanTreeSize, Span<byte> huffmanTree, Span<byte> huffmanTreeExtraBits, Span<byte> codeLengthBitDepth, Span<ushort> codeLengthBitDepthSymbols, JxlBitWriter writer)
    {
        for (int i = 0; i < huffmanTreeSize; ++i)
        {
            int ix = huffmanTree[i];
            writer.Write(codeLengthBitDepth[ix], codeLengthBitDepthSymbols[ix]);
            DebugGuard.MustBeLessThan(ix, 17, nameof(ix));

            // Extra bits
            //
            // Micro optimization:
            // Original:
            //   switch (ix)
            //   {
            //      case 16:
            //          writer->Write(2, huffman_tree_extra_bits[i]);
            //          break;
            //      case 17:
            //          writer->Write(3, huffman_tree_extra_bits[i]);
            //          break;
            //      default:
            //          // no-op
            //          break;
            //   }
            if ((ix & 16) != 0)
            {
                writer.Write(2 + (ix & 1), huffmanTreeExtraBits[i]);
            }
        }
    }

    public static void StoreSimpleHuffmanTree(Span<byte> depths, InlineArray4<int> symbols, int numSymbols, int maxBits, JxlBitWriter writer)
    {
        writer.Write(2, 1);
        writer.Write(2, numSymbols - 1);

        for (int i = 0; i < numSymbols; i++)
        {
            for (int j = i + 1; j < numSymbols; j++)
            {
                if (depths[symbols[j]] < depths[symbols[i]])
                {
                    RuntimeUtility.Swap(ref symbols[j], ref symbols[i]);
                }
            }
        }

        if (numSymbols == 2)
        {
            writer.Write(maxBits, symbols[0]);
            writer.Write(maxBits, symbols[1]);
        }
        else if (numSymbols == 3)
        {
            writer.Write(maxBits, symbols[0]);
            writer.Write(maxBits, symbols[1]);
            writer.Write(maxBits, symbols[2]);
        }
        else
        {
            writer.Write(maxBits, symbols[0]);
            writer.Write(maxBits, symbols[1]);
            writer.Write(maxBits, symbols[2]);
            writer.Write(maxBits, symbols[3]);
            writer.Write(1, depths[symbols[0]] == 1 ? 1 : 0);
        }
    }

    public static void StoreHuffmanTree(Span<byte> depths, int num, JxlBitWriter writer)
    {
        Span<byte> arena = stackalloc byte[2 * num];
        Span<byte> huffmanTree = arena;
        Span<byte> huffmanTreeExtraBits = arena[num..];
        int huffmanTreeSize = 0;
        JxlHuffmanTree.WriteHuffmanTree(depths, num, ref huffmanTreeSize, huffmanTree, huffmanTreeExtraBits);

        Span<int> huffmanTreeHistogram = stackalloc int[CodeLengthCodes];
        huffmanTreeHistogram.Clear();

        for (int i = 0; i < huffmanTreeSize; ++i)
        {
            huffmanTreeHistogram[huffmanTree[i]]++;
        }

        int numCodes = 0;
        int code = 0;

        for (int i = 0; i < CodeLengthCodes; ++i)
        {
            if (huffmanTreeHistogram[i] != 0)
            {
                if (numCodes == 0)
                {
                    code = i;
                    numCodes = 1;
                }
                else if (numCodes == 1)
                {
                    numCodes = 2;
                    break;
                }
            }
        }

        Span<byte> codeLengthBitDepth = stackalloc byte[CodeLengthCodes];
        Span<short> codeLengthBitDepthSymbols = stackalloc short[CodeLengthCodes];
        codeLengthBitDepth.Clear();
        codeLengthBitDepthSymbols.Clear();

        JxlHuffmanTree.CreateHuffmanTree(huffmanTreeHistogram, CodeLengthCodes, 5, codeLengthBitDepth);
        JxlHuffmanTree.ConvertBitDepthsToSymbols(codeLengthBitDepth, CodeLengthCodes, codeLengthBitDepthSymbols);

        StoreHuffmanTreeOfHuffmanTreeToBitMask(numCodes, codeLengthBitDepth, writer);

        if (numCodes == 1)
        {
            codeLengthBitDepth[code] = 0;
        }

        StoreHuffmanTreeToBitMask(huffmanTreeSize, huffmanTree, huffmanTreeExtraBits, codeLengthBitDepth, codeLengthBitDepthSymbols, writer);
    }

    public static void BuildAndStoreHuffmanTree(Span<int> histogram, int length, Span<byte> depth, Span<short> bits, JxlBitWriter writer)
    {
        int count = 0;
        InlineArray4<int> s4 = default;

        for (int i = 0; i < length; i++)
        {
            if (histogram[i] != 0)
            {
                if (count < 4)
                {
                    s4[count] = i;
                }
                else if (count > 4)
                {
                    break;
                }

                count++;
            }
        }

        int maxBitsCounter = length - 1;
        int maxBits = 0;

        while (maxBitsCounter != 0)
        {
            maxBitsCounter >>= 1;
            ++maxBits;
        }

        if (count <= 1)
        {
            writer.Write(4, 1);
            writer.Write(maxBits, s4[0]);
            return;
        }

        JxlHuffmanTree.CreateHuffmanTree(histogram, length, 15, depth);
        JxlHuffmanTree.ConvertBitDepthsToSymbols(depth, length, bits);

        if (count <= 4)
        {
            StoreSimpleHuffmanTree(depth, s4, count, maxBits, writer);
        }
        else
        {
            StoreHuffmanTree(depth, length, writer);
        }
    }
}
