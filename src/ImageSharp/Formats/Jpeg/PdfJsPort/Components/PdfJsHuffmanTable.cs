// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// Represents a Huffman Table
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct PdfJsHuffmanTable
    {
        /// <summary>
        /// Gets the max code array
        /// </summary>
        public FixedInt64Buffer18 MaxCode;

        /// <summary>
        /// Gets the value offset array
        /// </summary>
        public FixedInt16Buffer18 ValOffset;

        /// <summary>
        /// Gets the huffman value array
        /// </summary>
        public FixedByteBuffer256 HuffVal;

        /// <summary>
        /// Gets the lookahead array
        /// </summary>
        public FixedInt16Buffer256 Lookahead;

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfJsHuffmanTable"/> struct.
        /// </summary>
        /// <param name="memoryManager">The <see cref="MemoryManager"/> to use for buffer allocations.</param>
        /// <param name="lengths">The code lengths</param>
        /// <param name="values">The huffman values</param>
        public PdfJsHuffmanTable(MemoryManager memoryManager, ReadOnlySpan<byte> lengths, ReadOnlySpan<byte> values)
        {
            const int length = 257;
            using (IBuffer<short> huffsize = memoryManager.Allocate<short>(length))
            using (IBuffer<short> huffcode = memoryManager.Allocate<short>(length))
            {
                ref short huffsizeRef = ref MemoryMarshal.GetReference(huffsize.Span);
                ref short huffcodeRef = ref MemoryMarshal.GetReference(huffcode.Span);

                GenerateSizeTable(lengths, ref huffsizeRef);
                GenerateCodeTable(ref huffsizeRef, ref huffcodeRef, length);
                this.GenerateDecoderTables(lengths, ref huffcodeRef);
                this.GenerateLookaheadTables(lengths, values, ref huffcodeRef);
            }

            fixed (byte* huffValRef = this.HuffVal.Data)
            {
                var huffValSpan = new Span<byte>(huffValRef, 256);

                values.CopyTo(huffValSpan);
            }
        }

        /// <summary>
        /// Figure C.1: make table of Huffman code length for each symbol
        /// </summary>
        /// <param name="lengths">The code lengths</param>
        /// <param name="huffsizeRef">The huffman size span ref</param>
        private static void GenerateSizeTable(ReadOnlySpan<byte> lengths, ref short huffsizeRef)
        {
            short index = 0;
            for (short l = 1; l <= 16; l++)
            {
                byte i = lengths[l];
                for (short j = 0; j < i; j++)
                {
                    Unsafe.Add(ref huffsizeRef, index) = l;
                    index++;
                }
            }

            Unsafe.Add(ref huffsizeRef, index) = 0;
        }

        /// <summary>
        /// Figure C.2: generate the codes themselves
        /// </summary>
        /// <param name="huffsizeRef">The huffman size span ref</param>
        /// <param name="huffcodeRef">The huffman code span ref</param>
        /// <param name="length">The length of the huffsize span</param>
        private static void GenerateCodeTable(ref short huffsizeRef, ref short huffcodeRef, int length)
        {
            short k = 0;
            short si = huffsizeRef;
            short code = 0;
            for (short i = 0; i < length; i++)
            {
                while (Unsafe.Add(ref huffsizeRef, k) == si)
                {
                    Unsafe.Add(ref huffcodeRef, k) = code;
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
        /// <param name="huffcodeRef">The huffman code span ref</param>
        private void GenerateDecoderTables(ReadOnlySpan<byte> lengths, ref short huffcodeRef)
        {
            fixed (short* valOffsetRef = this.ValOffset.Data)
            fixed (long* maxcodeRef = this.MaxCode.Data)
            {
                short bitcount = 0;
                for (int i = 1; i <= 16; i++)
                {
                    if (lengths[i] != 0)
                    {
                        // valOffsetRef[l] = huffcodeRef[] index of 1st symbol of code length i, minus the minimum code of length i
                        valOffsetRef[i] = (short)(bitcount - Unsafe.Add(ref huffcodeRef, bitcount));
                        bitcount += lengths[i];
                        maxcodeRef[i] = Unsafe.Add(ref huffcodeRef, bitcount - 1); // maximum code of length i
                    }
                    else
                    {
                        maxcodeRef[i] = -1; // -1 if no codes of this length
                    }
                }

                valOffsetRef[17] = 0;
                maxcodeRef[17] = 0xFFFFFL;
            }
        }

        /// <summary>
        /// Generates lookup tables to speed up decoding
        /// </summary>
        /// <param name="lengths">The code lengths</param>
        /// <param name="huffval">The huffman value array</param>
        /// <param name="huffcodeRef">The huffman code span ref</param>
        private void GenerateLookaheadTables(ReadOnlySpan<byte> lengths, ReadOnlySpan<byte> huffval, ref short huffcodeRef)
        {
            // TODO: This generation code matches the libJpeg code but the lookahead table is not actually used yet.
            // To use it we need to implement fast lookup path in PdfJsScanDecoder.DecodeHuffman
            // This should yield much faster scan decoding as usually, more than 95% of the Huffman codes
            // will be 8 or fewer bits long and can be handled without looping.
            fixed (short* lookaheadRef = this.Lookahead.Data)
            {
                var lookaheadSpan = new Span<short>(lookaheadRef, 256);

                lookaheadSpan.Fill(2034); // 9 << 8;

                int p = 0;
                for (int l = 1; l <= 8; l++)
                {
                    for (int i = 1; i <= lengths[l]; i++, p++)
                    {
                        // l = current code's length, p = its index in huffcode[] & huffval[].
                        // Generate left-justified code followed by all possible bit sequences
                        int lookBits = Unsafe.Add(ref huffcodeRef, p) << (8 - l);
                        for (int ctr = 1 << (8 - l); ctr > 0; ctr--)
                        {
                            lookaheadRef[lookBits] = (short)((l << 8) | huffval[p]);
                            lookBits++;
                        }
                    }
                }
            }
        }
    }
}