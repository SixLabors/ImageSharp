// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Represents a Huffman Table
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct HuffmanTable
    {
        private bool isDerived;

        private readonly MemoryAllocator memoryAllocator;

        /// <summary>
        /// Gets the sizes array
        /// </summary>
        public fixed byte Sizes[17];

        /// <summary>
        /// Gets the huffman value array.
        /// </summary>
        public fixed byte Values[256];

        /// <summary>
        /// Gets the max code array.
        /// </summary>
        public fixed ulong MaxCode[18];

        /// <summary>
        /// Gets the value offset array.
        /// </summary>
        public fixed int ValOffset[19];

        /// <summary>
        /// Gets the lookahead array.
        /// </summary>
        public fixed byte LookaheadSize[HuffmanScanDecoder.JpegHuffLookupSize];

        /// <summary>
        /// Gets the lookahead array.
        /// </summary>
        public fixed byte LookaheadValue[HuffmanScanDecoder.JpegHuffLookupSize];

        /// <summary>
        /// Initializes a new instance of the <see cref="HuffmanTable"/> struct.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations.</param>
        /// <param name="codeLengths">The code lengths</param>
        /// <param name="values">The huffman values</param>
        public HuffmanTable(MemoryAllocator memoryAllocator, ReadOnlySpan<byte> codeLengths, ReadOnlySpan<byte> values)
        {
            this.isDerived = false;
            this.memoryAllocator = memoryAllocator;
            Unsafe.CopyBlockUnaligned(ref this.Sizes[0], ref MemoryMarshal.GetReference(codeLengths), (uint)codeLengths.Length);
            Unsafe.CopyBlockUnaligned(ref this.Values[0], ref MemoryMarshal.GetReference(values), (uint)values.Length);
        }

        /// <summary>
        /// Expands the HuffmanTable into its derived form.
        /// </summary>
        public void Derive()
        {
            if (this.isDerived)
            {
                return;
            }

            int p, si;
            Span<char> huffsize = stackalloc char[257];
            Span<uint> huffcode = stackalloc uint[257];
            uint code;

            // Figure C.1: make table of Huffman code length for each symbol
            p = 0;
            for (int l = 1; l <= 16; l++)
            {
                int i = this.Sizes[l];
                while (i-- != 0)
                {
                    huffsize[p++] = (char)l;
                }
            }

            huffsize[p] = (char)0;

            // Figure C.2: generate the codes themselves
            code = 0;
            si = huffsize[0];
            p = 0;
            while (huffsize[p] != 0)
            {
                while (huffsize[p] == si)
                {
                    huffcode[p++] = code;
                    code++;
                }

                code <<= 1;
                si++;
            }

            // Figure F.15: generate decoding tables for bit-sequential decoding
            p = 0;
            for (int l = 1; l <= 16; l++)
            {
                if (this.Sizes[l] != 0)
                {
                    int offset = p - (int)huffcode[p];
                    this.ValOffset[l] = offset;
                    p += this.Sizes[l];
                    this.MaxCode[l] = huffcode[p - 1]; // Maximum code of length l
                    this.MaxCode[l] <<= 64 - l; // left justify
                    this.MaxCode[l] |= (1ul << (64 - l)) - 1;
                }
                else
                {
                    this.MaxCode[l] = 0; // TODO: should be -1 if no codes of this length
                }
            }

            this.ValOffset[18] = 0;
            this.MaxCode[17] = ulong.MaxValue; // ensures huff decode terminates

            // Compute lookahead tables to speed up decoding.
            // First we set all the table entries to 0, indicating "too long";
            // then we iterate through the Huffman codes that are short enough and
            // fill in all the entries that correspond to bit sequences starting
            // with that code.
            ref byte lookupSizeRef = ref this.LookaheadSize[0];
            Unsafe.InitBlockUnaligned(ref lookupSizeRef, HuffmanScanDecoder.JpegHuffLookupBits + 1, HuffmanScanDecoder.JpegHuffLookupSize);

            p = 0;
            for (int length = 1; length <= HuffmanScanDecoder.JpegHuffLookupBits; length++)
            {
                for (int i = 1; i <= this.Sizes[length]; i++, p++)
                {
                    // length = current code's length, p = its index in huffcode[] & huffval[].
                    // Generate left-justified code followed by all possible bit sequences
                    int lookbits = (int)(huffcode[p] << (HuffmanScanDecoder.JpegHuffLookupBits - length));
                    for (int ctr = 1 << (HuffmanScanDecoder.JpegHuffLookupBits - length); ctr > 0; ctr--)
                    {
                        this.LookaheadSize[lookbits] = (byte)length;
                        this.LookaheadValue[lookbits] = this.Values[p];
                        lookbits++;
                    }
                }
            }

            this.isDerived = true;
        }
    }
}