// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
    /// <summary>
    /// Represents a Huffman tree
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct OrigHuffmanTree
    {
        /// <summary>
        /// The index of the AC table row
        /// </summary>
        public const int AcTableIndex = 1;

        /// <summary>
        /// The index of the DC table row
        /// </summary>
        public const int DcTableIndex = 0;

        /// <summary>
        /// The maximum (inclusive) number of codes in a Huffman tree.
        /// </summary>
        public const int MaxNCodes = 256;

        /// <summary>
        /// The maximum (inclusive) number of bits in a Huffman code.
        /// </summary>
        public const int MaxCodeLength = 16;

        /// <summary>
        /// The maximum number of Huffman table classes
        /// </summary>
        public const int MaxTc = 1;

        /// <summary>
        /// The maximum number of Huffman table identifiers
        /// </summary>
        public const int MaxTh = 3;

        /// <summary>
        /// Row size of the Huffman table
        /// </summary>
        public const int ThRowSize = MaxTh + 1;

        /// <summary>
        /// Number of Hufman Trees in the Huffman table
        /// </summary>
        public const int NumberOfTrees = (MaxTc + 1) * (MaxTh + 1);

        /// <summary>
        /// The log-2 size of the Huffman decoder's look-up table.
        /// </summary>
        public const int LutSizeLog2 = 8;

        /// <summary>
        /// Gets or sets the number of codes in the tree.
        /// </summary>
        public int Length;

        /// <summary>
        /// Gets the look-up table for the next LutSize bits in the bit-stream.
        /// The high 8 bits of the uint16 are the encoded value. The low 8 bits
        /// are 1 plus the code length, or 0 if the value is too large to fit in
        /// lutSize bits.
        /// </summary>
        public FixedInt32Buffer256 Lut;

        /// <summary>
        /// Gets the the decoded values, sorted by their encoding.
        /// </summary>
        public FixedInt32Buffer256 Values;

        /// <summary>
        /// Gets the array of minimum codes.
        /// MinCodes[i] is the minimum code of length i, or -1 if there are no codes of that length.
        /// </summary>
        public FixedInt32Buffer16 MinCodes;

        /// <summary>
        /// Gets the array of maximum codes.
        /// MaxCodes[i] is the maximum code of length i, or -1 if there are no codes of that length.
        /// </summary>
        public FixedInt32Buffer16 MaxCodes;

        /// <summary>
        /// Gets the array of indices. Indices[i] is the index into Values of MinCodes[i].
        /// </summary>
        public FixedInt32Buffer16 Indices;

        /// <summary>
        /// Creates and initializes an array of <see cref="OrigHuffmanTree" /> instances of size <see cref="NumberOfTrees" />
        /// </summary>
        /// <returns>An array of <see cref="OrigHuffmanTree" /> instances representing the Huffman tables</returns>
        public static OrigHuffmanTree[] CreateHuffmanTrees()
        {
            return new OrigHuffmanTree[NumberOfTrees];
        }

        /// <summary>
        /// Internal part of the DHT processor, whatever does it mean
        /// </summary>
        /// <param name="inputProcessor">The decoder instance</param>
        /// <param name="defineHuffmanTablesData">The temporal buffer that holds the data that has been read from the Jpeg stream</param>
        /// <param name="remaining">Remaining bits</param>
        public void ProcessDefineHuffmanTablesMarkerLoop(
            ref InputProcessor inputProcessor,
            byte[] defineHuffmanTablesData,
            ref int remaining)
        {
            // Read nCodes and huffman.Valuess (and derive h.Length).
            // nCodes[i] is the number of codes with code length i.
            // h.Length is the total number of codes.
            this.Length = 0;

            int[] ncodes = new int[MaxCodeLength];
            for (int i = 0; i < ncodes.Length; i++)
            {
                ncodes[i] = defineHuffmanTablesData[i + 1];
                this.Length += ncodes[i];
            }

            if (this.Length == 0)
            {
                throw new ImageFormatException("Huffman table has zero length");
            }

            if (this.Length > MaxNCodes)
            {
                throw new ImageFormatException("Huffman table has excessive length");
            }

            remaining -= this.Length + 17;
            if (remaining < 0)
            {
                throw new ImageFormatException("DHT has wrong length");
            }

            byte[] values = new byte[MaxNCodes];
            inputProcessor.ReadFull(values, 0, this.Length);

            fixed (int* valuesPtr = this.Values.Data)
            fixed (int* lutPtr = this.Lut.Data)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    valuesPtr[i] = values[i];
                }

                // Derive the look-up table.
                for (int i = 0; i < MaxNCodes; i++)
                {
                    lutPtr[i] = 0;
                }

                int x = 0, code = 0;

                for (int i = 0; i < LutSizeLog2; i++)
                {
                    code <<= 1;

                    for (int j = 0; j < ncodes[i]; j++)
                    {
                        // The codeLength is 1+i, so shift code by 8-(1+i) to
                        // calculate the high bits for every 8-bit sequence
                        // whose codeLength's high bits matches code.
                        // The high 8 bits of lutValue are the encoded value.
                        // The low 8 bits are 1 plus the codeLength.
                        int base2 = code << (7 - i);
                        int lutValue = (valuesPtr[x] << 8) | (2 + i);

                        for (int k = 0; k < 1 << (7 - i); k++)
                        {
                            lutPtr[base2 | k] = lutValue;
                        }

                        code++;
                        x++;
                    }
                }
            }

            fixed (int* minCodesPtr = this.MinCodes.Data)
            fixed (int* maxCodesPtr = this.MaxCodes.Data)
            fixed (int* indicesPtr = this.Indices.Data)
            {
                // Derive minCodes, maxCodes, and indices.
                int c = 0, index = 0;
                for (int i = 0; i < ncodes.Length; i++)
                {
                    int nc = ncodes[i];
                    if (nc == 0)
                    {
                        minCodesPtr[i] = -1;
                        maxCodesPtr[i] = -1;
                        indicesPtr[i] = -1;
                    }
                    else
                    {
                        minCodesPtr[i] = c;
                        maxCodesPtr[i] = c + nc - 1;
                        indicesPtr[i] = index;
                        c += nc;
                        index += nc;
                    }

                    c <<= 1;
                }
            }
        }

        /// <summary>
        /// Gets the value for the given code and index.
        /// </summary>
        /// <param name="code">The code</param>
        /// <param name="codeLength">The code length</param>
        /// <returns>The value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetValue(int code, int codeLength)
        {
            return this.Values[this.Indices[codeLength] + code - this.MinCodes[codeLength]];
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FixedInt32Buffer256
        {
            public fixed int Data[256];

            public int this[int idx]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    ref int self = ref Unsafe.As<FixedInt32Buffer256, int>(ref this);
                    return Unsafe.Add(ref self, idx);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FixedInt32Buffer16
        {
            public fixed int Data[16];

            public int this[int idx]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    ref int self = ref Unsafe.As<FixedInt32Buffer16, int>(ref this);
                    return Unsafe.Add(ref self, idx);
                }
            }
        }
    }
}