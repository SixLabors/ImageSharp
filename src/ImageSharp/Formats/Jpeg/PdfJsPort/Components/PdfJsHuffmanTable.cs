// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// Represents a Huffman Table
    /// </summary>
    internal struct PdfJsHuffmanTable : IDisposable
    {
        private BasicArrayBuffer<short> lookahead;
        private BasicArrayBuffer<short> valOffset;
        private BasicArrayBuffer<long> maxcode;
        private IManagedByteBuffer huffval;

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfJsHuffmanTable"/> struct.
        /// </summary>
        /// <param name="memoryManager">The <see cref="MemoryManager"/> to use for buffer allocations.</param>
        /// <param name="lengths">The code lengths</param>
        /// <param name="values">The huffman values</param>
        public PdfJsHuffmanTable(MemoryManager memoryManager, byte[] lengths, byte[] values)
        {
            // TODO: Replace FakeBuffer<T> usages with standard or array orfixed-sized arrays
            this.lookahead = memoryManager.AllocateFake<short>(256);
            this.valOffset = memoryManager.AllocateFake<short>(18);
            this.maxcode = memoryManager.AllocateFake<long>(18);

            using (IBuffer<short> huffsize = memoryManager.Allocate<short>(257))
            using (IBuffer<short> huffcode = memoryManager.Allocate<short>(257))
            {
                GenerateSizeTable(lengths, huffsize.Span);
                GenerateCodeTable(huffsize.Span, huffcode.Span);
                GenerateDecoderTables(lengths, huffcode.Span, this.valOffset.Span, this.maxcode.Span);
                GenerateLookaheadTables(lengths, values, this.lookahead.Span);
            }

            this.huffval = memoryManager.AllocateManagedByteBuffer(values.Length, true);
            Buffer.BlockCopy(values, 0, this.huffval.Array, 0, values.Length);

            this.MaxCode = this.maxcode.Array;
            this.ValOffset = this.valOffset.Array;
            this.HuffVal = this.huffval.Array;
            this.Lookahead = this.lookahead.Array;
        }

        /// <summary>
        /// Gets the max code array
        /// </summary>
        public long[] MaxCode
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <summary>
        /// Gets the value offset array
        /// </summary>
        public short[] ValOffset
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <summary>
        /// Gets the huffman value array
        /// </summary>
        public byte[] HuffVal
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <summary>
        /// Gets the lookahead array
        /// </summary>
        public short[] Lookahead
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.lookahead?.Dispose();
            this.valOffset?.Dispose();
            this.maxcode?.Dispose();
            this.huffval?.Dispose();

            this.lookahead = null;
            this.valOffset = null;
            this.maxcode = null;
            this.huffval = null;
        }

        /// <summary>
        /// Figure C.1: make table of Huffman code length for each symbol
        /// </summary>
        /// <param name="lengths">The code lengths</param>
        /// <param name="huffsize">The huffman size span</param>
        private static void GenerateSizeTable(byte[] lengths, Span<short> huffsize)
        {
            short index = 0;
            for (short l = 1; l <= 16; l++)
            {
                byte i = lengths[l];
                for (short j = 0; j < i; j++)
                {
                    huffsize[index] = l;
                    index++;
                }
            }

            huffsize[index] = 0;
        }

        /// <summary>
        /// Figure C.2: generate the codes themselves
        /// </summary>
        /// <param name="huffsize">The huffman size span</param>
        /// <param name="huffcode">The huffman code span</param>
        private static void GenerateCodeTable(Span<short> huffsize, Span<short> huffcode)
        {
            short k = 0;
            short si = huffsize[0];
            short code = 0;
            for (short i = 0; i < huffsize.Length; i++)
            {
                while (huffsize[k] == si)
                {
                    huffcode[k] = code;
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
        /// <param name="lengths">The code lengths</param>
        /// <param name="huffcode">The huffman code span</param>
        /// <param name="valOffset">The value offset span</param>
        /// <param name="maxcode">The max code span</param>
        private static void GenerateDecoderTables(byte[] lengths, Span<short> huffcode, Span<short> valOffset, Span<long> maxcode)
        {
            short bitcount = 0;
            for (int i = 1; i <= 16; i++)
            {
                if (lengths[i] != 0)
                {
                    // valoffset[l] = huffval[] index of 1st symbol of code length i,
                    // minus the minimum code of length i
                    valOffset[i] = (short)(bitcount - huffcode[bitcount]);
                    bitcount += lengths[i];
                    maxcode[i] = huffcode[bitcount - 1]; // maximum code of length i
                }
                else
                {
                    maxcode[i] = -1; // -1 if no codes of this length
                }
            }

            valOffset[17] = 0;
            maxcode[17] = 0xFFFFFL;
        }

        /// <summary>
        /// Generates lookup tables to speed up decoding
        /// </summary>
        /// <param name="lengths">The code lengths</param>
        /// <param name="huffval">The huffman value array</param>
        /// <param name="lookahead">The lookahead span</param>
        private static void GenerateLookaheadTables(byte[] lengths, byte[] huffval, Span<short> lookahead)
        {
            int x = 0, code = 0;

            for (int i = 0; i < 8; i++)
            {
                code <<= 1;

                for (int j = 0; j < lengths[i + 1]; j++)
                {
                    // The codeLength is 1+i, so shift code by 8-(1+i) to
                    // calculate the high bits for every 8-bit sequence
                    // whose codeLength's high bits matches code.
                    // The high 8 bits of lutValue are the encoded value.
                    // The low 8 bits are 1 plus the codeLength.
                    byte base2 = (byte)(code << (7 - i));
                    short lutValue = (short)((short)(huffval[x] << 8) | (short)(2 + i));

                    for (int k = 0; k < 1 << (7 - i); k++)
                    {
                        lookahead[base2 | k] = lutValue;
                    }

                    code++;
                    x++;
                }
            }
        }
    }
}