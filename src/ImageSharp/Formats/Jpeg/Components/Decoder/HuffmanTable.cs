// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Represents a Huffman coding table containing basic coding data plus tables for accelerated computation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct HuffmanTable
    {
        private bool isConfigured;

        /// <summary>
        /// Derived from the DHT marker. Sizes[k] = # of symbols with codes of length k bits; Sizes[0] is unused.
        /// </summary>
        public fixed byte Sizes[17];

        /// <summary>
        /// Derived from the DHT marker. Contains the symbols, in order of incremental code length.
        /// </summary>
        public fixed byte Values[256];

        /// <summary>
        /// Contains the largest code of length k (0 if none). MaxCode[17] is a sentinel to
        /// ensure <see cref="HuffmanScanBuffer.DecodeHuffman"/> terminates.
        /// </summary>
        public fixed ulong MaxCode[18];

        /// <summary>
        /// Values[] offset for codes of length k  ValOffset[k] = Values[] index of 1st symbol of code length
        /// k, less the smallest code of length k; so given a code of length k, the corresponding symbol is
        /// Values[code + ValOffset[k]].
        /// </summary>
        public fixed int ValOffset[19];

        /// <summary>
        /// Contains the length of bits for the given k value.
        /// </summary>
        public fixed byte LookaheadSize[JpegConstants.Huffman.LookupSize];

        /// <summary>
        /// Lookahead table: indexed by the next <see cref="JpegConstants.Huffman.LookupBits"/> bits of
        /// the input data stream.  If the next Huffman code is no more
        /// than <see cref="JpegConstants.Huffman.LookupBits"/> bits long, we can obtain its length and
        /// the corresponding symbol directly from this tables.
        ///
        /// The lower 8 bits of each table entry contain the number of
        /// bits in the corresponding Huffman code, or <see cref="JpegConstants.Huffman.LookupBits"/> + 1
        /// if too long.  The next 8 bits of each entry contain the symbol.
        /// </summary>
        public fixed byte LookaheadValue[JpegConstants.Huffman.LookupSize];

        /// <summary>
        /// Initializes a new instance of the <see cref="HuffmanTable"/> struct.
        /// </summary>
        /// <param name="codeLengths">The code lengths</param>
        /// <param name="values">The huffman values</param>
        public HuffmanTable(ReadOnlySpan<byte> codeLengths, ReadOnlySpan<byte> values)
        {
            this.isConfigured = false;
            Unsafe.CopyBlockUnaligned(ref this.Sizes[0], ref MemoryMarshal.GetReference(codeLengths), (uint)codeLengths.Length);
            Unsafe.CopyBlockUnaligned(ref this.Values[0], ref MemoryMarshal.GetReference(values), (uint)values.Length);
        }

        /// <summary>
        /// Expands the HuffmanTable into its readable form.
        /// </summary>
        public void Configure()
        {
            if (this.isConfigured)
            {
                return;
            }

            Span<char> huffSize = stackalloc char[257];
            Span<uint> huffCode = stackalloc uint[257];

            // Figure C.1: make table of Huffman code length for each symbol
            int p = 0;
            for (int j = 1; j <= 16; j++)
            {
                int i = this.Sizes[j];
                while (i-- != 0)
                {
                    huffSize[p++] = (char)j;
                }
            }

            huffSize[p] = (char)0;

            // Figure C.2: generate the codes themselves
            uint code = 0;
            int si = huffSize[0];
            p = 0;
            while (huffSize[p] != 0)
            {
                while (huffSize[p] == si)
                {
                    huffCode[p++] = code;
                    code++;
                }

                code <<= 1;
                si++;
            }

            // Figure F.15: generate decoding tables for bit-sequential decoding
            p = 0;
            for (int j = 1; j <= 16; j++)
            {
                if (this.Sizes[j] != 0)
                {
                    this.ValOffset[j] = p - (int)huffCode[p];
                    p += this.Sizes[j];
                    this.MaxCode[j] = huffCode[p - 1]; // Maximum code of length l
                    this.MaxCode[j] <<= JpegConstants.Huffman.RegisterSize - j; // Left justify
                    this.MaxCode[j] |= (1ul << (JpegConstants.Huffman.RegisterSize - j)) - 1;
                }
                else
                {
                    this.MaxCode[j] = 0;
                }
            }

            this.ValOffset[18] = 0;
            this.MaxCode[17] = ulong.MaxValue; // Ensures huff decode terminates

            // Compute lookahead tables to speed up decoding.
            // First we set all the table entries to JpegConstants.Huffman.SlowBits, indicating "too long";
            // then we iterate through the Huffman codes that are short enough and
            // fill in all the entries that correspond to bit sequences starting
            // with that code.
            ref byte lookupSizeRef = ref this.LookaheadSize[0];
            Unsafe.InitBlockUnaligned(ref lookupSizeRef, JpegConstants.Huffman.SlowBits, JpegConstants.Huffman.LookupSize);

            p = 0;
            for (int length = 1; length <= JpegConstants.Huffman.LookupBits; length++)
            {
                int jShift = JpegConstants.Huffman.LookupBits - length;
                for (int i = 1; i <= this.Sizes[length]; i++, p++)
                {
                    // length = current code's length, p = its index in huffCode[] & Values[].
                    // Generate left-justified code followed by all possible bit sequences
                    int lookBits = (int)(huffCode[p] << jShift);
                    for (int ctr = 1 << (JpegConstants.Huffman.LookupBits - length); ctr > 0; ctr--)
                    {
                        this.LookaheadSize[lookBits] = (byte)length;
                        this.LookaheadValue[lookBits] = this.Values[p];
                        lookBits++;
                    }
                }
            }

            this.isConfigured = true;
        }
    }
}
