// <copyright file="HuffmanTable.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using System;
    using System.Runtime.CompilerServices;

    using ImageSharp.Memory;

    /// <summary>
    /// Represents a Huffman Table
    /// </summary>
    internal struct HuffmanTable : IDisposable
    {
        private Buffer<short> lookahead;
        private Buffer<short> huffcode;
        private Buffer<short> huffsize;
        private Buffer<short> valOffset;
        private Buffer<long> maxcode;

        private Buffer<byte> huffval;
        private Buffer<byte> bits;

        /// <summary>
        /// Initializes a new instance of the <see cref="HuffmanTable"/> struct.
        /// </summary>
        /// <param name="lengths">The code lengths</param>
        /// <param name="values">The huffman values</param>
        public HuffmanTable(byte[] lengths, byte[] values)
        {
            this.lookahead = Buffer<short>.CreateClean(256);
            this.huffcode = Buffer<short>.CreateClean(257);
            this.huffsize = Buffer<short>.CreateClean(257);
            this.valOffset = Buffer<short>.CreateClean(18);
            this.maxcode = Buffer<long>.CreateClean(18);

            this.huffval = Buffer<byte>.CreateClean(values.Length);
            Buffer.BlockCopy(values, 0, this.huffval.Array, 0, values.Length);

            this.bits = Buffer<byte>.CreateClean(lengths.Length);
            Buffer.BlockCopy(lengths, 0, this.bits.Array, 0, lengths.Length);

            this.GenerateSizeTable();
            this.GenerateCodeTable();
            this.GenerateDecoderTables();
            this.GenerateLookaheadTables();
        }

        /// <summary>
        /// Gets the Huffman value code at the given index
        /// </summary>
        /// <param name="i">The index</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short GetHuffVal(int i)
        {
            return this.huffval[i];
        }

        /// <summary>
        /// Gets the max code at the given index
        /// </summary>
        /// <param name="i">The index</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long GetMaxCode(int i)
        {
            return this.maxcode[i];
        }

        /// <summary>
        /// Gets the index to the locatation of the huffman value
        /// </summary>
        /// <param name="i">The index</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetValOffset(int i)
        {
            return this.valOffset[i];
        }

        /// <summary>
        /// Gets the look ahead table balue
        /// </summary>
        /// <param name="i">The index</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetLookAhead(int i)
        {
            return this.lookahead[i];
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.lookahead?.Dispose();
            this.huffcode?.Dispose();
            this.huffsize?.Dispose();
            this.valOffset?.Dispose();
            this.maxcode?.Dispose();
            this.huffval?.Dispose();
            this.bits?.Dispose();

            this.lookahead = null;
            this.huffcode = null;
            this.huffsize = null;
            this.valOffset = null;
            this.maxcode = null;
            this.huffval = null;
            this.bits = null;
        }

        /// <summary>
        /// Figure C.1: make table of Huffman code length for each symbol
        /// </summary>
        private void GenerateSizeTable()
        {
            short index = 0;
            for (short l = 1; l <= 16; l++)
            {
                byte i = this.bits[l];
                for (short j = 0; j < i; j++)
                {
                    this.huffsize[index] = l;
                    index++;
                }
            }

            this.huffsize[index] = 0;
        }

        /// <summary>
        /// Figure C.2: generate the codes themselves
        /// </summary>
        private void GenerateCodeTable()
        {
            short k = 0;
            short si = this.huffsize[0];
            short code = 0;
            for (short i = 0; i < this.huffsize.Length; i++)
            {
                while (this.huffsize[k] == si)
                {
                    this.huffcode[k] = code;
                    code++;
                    k++;
                }

                code <<= 1;
                si++;
            }
        }

        /// <summary>
        /// Figure F.15: generate decoding tables for bit-sequential decoding
        /// </summary>
        private void GenerateDecoderTables()
        {
            short bitcount = 0;
            for (int i = 1; i <= 16; i++)
            {
                if (this.bits[i] != 0)
                {
                    // valoffset[l] = huffval[] index of 1st symbol of code length i,
                    // minus the minimum code of length i
                    this.valOffset[i] = (short)(bitcount - this.huffcode[bitcount]);
                    bitcount += this.bits[i];
                    this.maxcode[i] = this.huffcode[bitcount - 1]; // maximum code of length i
                }
                else
                {
                    this.maxcode[i] = -1; // -1 if no codes of this length
                }
            }

            this.valOffset[17] = 0;
            this.maxcode[17] = 0xFFFFFL;
        }

        /// <summary>
        /// Generates lookup tables to speed up decoding
        /// </summary>
        private void GenerateLookaheadTables()
        {
            int x = 0, code = 0;

            for (int i = 0; i < 8; i++)
            {
                code <<= 1;

                for (int j = 0; j < this.bits[i + 1]; j++)
                {
                    // The codeLength is 1+i, so shift code by 8-(1+i) to
                    // calculate the high bits for every 8-bit sequence
                    // whose codeLength's high bits matches code.
                    // The high 8 bits of lutValue are the encoded value.
                    // The low 8 bits are 1 plus the codeLength.
                    byte base2 = (byte)(code << (7 - i));
                    short lutValue = (short)((short)(this.huffval[x] << 8) | (short)(2 + i));

                    for (int k = 0; k < 1 << (7 - i); k++)
                    {
                        this.lookahead[base2 | k] = lutValue;
                    }

                    code++;
                    x++;
                }
            }
        }
    }
}