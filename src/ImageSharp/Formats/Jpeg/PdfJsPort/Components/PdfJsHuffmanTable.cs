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
        public FixedByteBuffer256 Values;

        /// <summary>
        /// Gets the lookahead array
        /// </summary>
        public FixedByteBuffer512 Lookahead;

        /// <summary>
        /// Gets the sizes array
        /// </summary>
        public FixedInt16Buffer257 Sizes;

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfJsHuffmanTable"/> struct.
        /// </summary>
        /// <param name="memoryManager">The <see cref="MemoryManager"/> to use for buffer allocations.</param>
        /// <param name="count">The code lengths</param>
        /// <param name="values">The huffman values</param>
        public PdfJsHuffmanTable(MemoryManager memoryManager, ReadOnlySpan<byte> count, ReadOnlySpan<byte> values)
        {
            const int Length = 257;
            using (IBuffer<short> huffcode = memoryManager.Allocate<short>(Length))
            {
                // Span<short> codes = huffcode.Span;
                ref short huffcodeRef = ref MemoryMarshal.GetReference(huffcode.Span);

                this.GenerateSizeTable(count);

                //int k = 0;
                //fixed (short* sizesRef = this.Sizes.Data)
                //fixed (short* deltaRef = this.ValOffset.Data)
                //fixed (long* maxcodeRef = this.MaxCode.Data)
                //{
                //    uint code = 0;
                //    int j;
                //    for (j = 1; j <= 16; j++)
                //    {
                //        // Compute delta to add to code to compute symbol id.
                //        deltaRef[j] = (short)(k - code);
                //        if (sizesRef[k] == j)
                //        {
                //            while (sizesRef[k] == j)
                //            {
                //                codes[k++] = (short)code++;

                //                // Unsafe.Add(ref huffcodeRef, k++) = (short)code++;

                //                // TODO: Throw if invalid?
                //            }
                //        }

                //        // Compute largest code + 1 for this size. preshifted as neeed later.
                //        maxcodeRef[j] = code << (16 - j);
                //        code <<= 1;
                //    }

                //    maxcodeRef[j] = 0xFFFFFFFF;
                //}

                //fixed (short* lookaheadRef = this.Lookahead.Data)
                //{
                //    const int FastBits = ScanDecoder.FastBits;
                //    var fast = new Span<short>(lookaheadRef, 1 << FastBits);
                //    fast.Fill(255); // Flag for non-accelerated

                //    fixed (short* sizesRef = this.Sizes.Data)
                //    {
                //        for (int i = 0; i < k; i++)
                //        {
                //            int s = sizesRef[i];
                //            if (s <= ScanDecoder.FastBits)
                //            {
                //                int c = codes[i] << (FastBits - s);
                //                int m = 1 << (FastBits - s);
                //                for (int j = 0; j < m; j++)
                //                {
                //                    fast[c + j] = (byte)i;
                //                }
                //            }
                //        }
                //    }
                //}

                this.GenerateCodeTable(ref huffcodeRef, Length, out int k);
                this.GenerateDecoderTables(count, ref huffcodeRef);
                this.GenerateLookaheadTables(count, values, ref huffcodeRef, k);
            }

            fixed (byte* huffValRef = this.Values.Data)
            {
                var huffValSpan = new Span<byte>(huffValRef, 256);

                values.CopyTo(huffValSpan);
            }
        }

        /// <summary>
        /// Figure C.1: make table of Huffman code length for each symbol
        /// </summary>
        /// <param name="lengths">The code lengths</param>
        private void GenerateSizeTable(ReadOnlySpan<byte> lengths)
        {
            fixed (short* sizesRef = this.Sizes.Data)
            {
                short k = 0;
                for (short i = 1; i < 17; i++)
                {
                    byte l = lengths[i];
                    for (short j = 0; j < l; j++)
                    {
                        sizesRef[k] = i;
                        k++;
                    }
                }

                sizesRef[k] = 0;
            }
        }

        /// <summary>
        /// Figure C.2: generate the codes themselves
        /// </summary>
        /// <param name="huffcodeRef">The huffman code span ref</param>
        /// <param name="length">The length of the huffsize span</param>
        /// <param name="k">The length of any valid codes</param>
        private void GenerateCodeTable(ref short huffcodeRef, int length, out int k)
        {
            fixed (short* sizesRef = this.Sizes.Data)
            {
                k = 0;
                short si = sizesRef[0];
                short code = 0;
                for (short i = 0; i < length; i++)
                {
                    while (sizesRef[k] == si)
                    {
                        Unsafe.Add(ref huffcodeRef, k) = code;
                        code++;
                        k++;
                    }

                    code <<= 1;
                    si++;
                }
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
                        maxcodeRef[i] = Unsafe.Add(ref huffcodeRef, bitcount - 1) << (16 - i); // maximum code of length i preshifted for faster reading later
                    }
                    else
                    {
                        maxcodeRef[i] = -1; // -1 if no codes of this length
                    }
                }

                valOffsetRef[17] = 0;
                maxcodeRef[17] = 0xFFFFFFFFL;
            }
        }

        /// <summary>
        /// Generates non-spec lookup tables to speed up decoding
        /// </summary>
        /// <param name="lengths">The code lengths</param>
        /// <param name="huffval">The huffman value array</param>
        /// <param name="huffcodeRef">The huffman code span ref</param>
        /// <param name="k">The lengths of any valid codes</param>
        private void GenerateLookaheadTables(ReadOnlySpan<byte> lengths, ReadOnlySpan<byte> huffval, ref short huffcodeRef, int k)
        {
            // TODO: Rewrite this to match stb_Image
            // TODO: This generation code matches the libJpeg code but the lookahead table is not actually used yet.
            // To use it we need to implement fast lookup path in PdfJsScanDecoder.DecodeHuffman
            // This should yield much faster scan decoding as usually, more than 95% of the Huffman codes
            // will be 8 or fewer bits long and can be handled without looping.
            fixed (byte* lookaheadRef = this.Lookahead.Data)
            {
                const int FastBits = ScanDecoder.FastBits;
                var lookaheadSpan = new Span<short>(lookaheadRef, 1 << ScanDecoder.FastBits);

                lookaheadSpan.Fill(255); // Flag for non-accelerated
                fixed (short* sizesRef = this.Sizes.Data)
                {
                    for (int i = 0; i < k; ++i)
                    {
                        int s = sizesRef[i];
                        if (s <= ScanDecoder.FastBits)
                        {
                            int c = Unsafe.Add(ref huffcodeRef, i) << (FastBits - s);
                            int m = 1 << (FastBits - s);
                            for (int j = 0; j < m; ++j)
                            {
                                lookaheadRef[c + j] = (byte)i;
                            }
                        }
                    }
                }
            }
        }
    }
}