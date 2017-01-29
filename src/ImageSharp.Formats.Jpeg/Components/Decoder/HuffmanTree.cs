// <copyright file="HuffmanTree.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Formats.Jpg
{
    using System;
    using System.Buffers;

    /// <summary>
    /// Represents a Huffman tree
    /// </summary>
    internal struct HuffmanTree : IDisposable
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
        public int[] Lut;

        /// <summary>
        /// Gets the the decoded values, sorted by their encoding.
        /// </summary>
        public int[] Values;

        /// <summary>
        /// Gets the array of minimum codes.
        /// MinCodes[i] is the minimum code of length i, or -1 if there are no codes of that length.
        /// </summary>
        public int[] MinCodes;

        /// <summary>
        /// Gets the array of maximum codes.
        /// MaxCodes[i] is the maximum code of length i, or -1 if there are no codes of that length.
        /// </summary>
        public int[] MaxCodes;

        /// <summary>
        /// Gets the array of indices. Indices[i] is the index into Values of MinCodes[i].
        /// </summary>
        public int[] Indices;

        private static readonly ArrayPool<int> IntPool256 = ArrayPool<int>.Create(MaxNCodes, 50);

        private static readonly ArrayPool<byte> BytePool256 = ArrayPool<byte>.Create(MaxNCodes, 50);

        private static readonly ArrayPool<int> CodesPool16 = ArrayPool<int>.Create(MaxCodeLength, 50);

        /// <summary>
        /// Creates and initializes an array of <see cref="HuffmanTree" /> instances of size <see cref="NumberOfTrees" />
        /// </summary>
        /// <returns>An array of <see cref="HuffmanTree" /> instances representing the Huffman tables</returns>
        public static HuffmanTree[] CreateHuffmanTrees()
        {
            HuffmanTree[] result = new HuffmanTree[NumberOfTrees];
            for (int i = 0; i < MaxTc + 1; i++)
            {
                for (int j = 0; j < MaxTh + 1; j++)
                {
                    result[(i * ThRowSize) + j].Init();
                }
            }

            return result;
        }

        /// <summary>
        /// Disposes the underlying buffers
        /// </summary>
        public void Dispose()
        {
            IntPool256.Return(this.Lut, true);
            IntPool256.Return(this.Values, true);
            CodesPool16.Return(this.MinCodes, true);
            CodesPool16.Return(this.MaxCodes, true);
            CodesPool16.Return(this.Indices, true);
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

            byte[] values = null;
            try
            {
                values = BytePool256.Rent(MaxNCodes);
                inputProcessor.ReadFull(values, 0, this.Length);

                for (int i = 0; i < values.Length; i++)
                {
                    this.Values[i] = values[i];
                }
            }
            finally
            {
                BytePool256.Return(values, true);
            }

            // Derive the look-up table.
            for (int i = 0; i < this.Lut.Length; i++)
            {
                this.Lut[i] = 0;
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
                    int lutValue = (this.Values[x] << 8) | (2 + i);

                    for (int k = 0; k < 1 << (7 - i); k++)
                    {
                        this.Lut[base2 | k] = lutValue;
                    }

                    code++;
                    x++;
                }
            }

            // Derive minCodes, maxCodes, and indices.
            int c = 0, index = 0;
            for (int i = 0; i < ncodes.Length; i++)
            {
                int nc = ncodes[i];
                if (nc == 0)
                {
                    this.MinCodes[i] = -1;
                    this.MaxCodes[i] = -1;
                    this.Indices[i] = -1;
                }
                else
                {
                    this.MinCodes[i] = c;
                    this.MaxCodes[i] = c + nc - 1;
                    this.Indices[i] = index;
                    c += nc;
                    index += nc;
                }

                c <<= 1;
            }
        }

        /// <summary>
        /// Gets the value for the given code and index.
        /// </summary>
        /// <param name="code">The code</param>
        /// <param name="codeLength">The code length</param>
        /// <returns>The value</returns>
        public int GetValue(int code, int codeLength)
        {
            return this.Values[this.Indices[codeLength] + code - this.MinCodes[codeLength]];
        }

        /// <summary>
        /// Initializes the Huffman tree
        /// </summary>
        private void Init()
        {
            this.Lut = IntPool256.Rent(MaxNCodes);
            this.Values = IntPool256.Rent(MaxNCodes);
            this.MinCodes = CodesPool16.Rent(MaxCodeLength);
            this.MaxCodes = CodesPool16.Rent(MaxCodeLength);
            this.Indices = CodesPool16.Rent(MaxCodeLength);
        }
    }
}