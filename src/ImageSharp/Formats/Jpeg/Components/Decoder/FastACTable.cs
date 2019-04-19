// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    internal unsafe struct FastACTable
    {
        /// <summary>
        /// Gets the lookahead array.
        /// </summary>
        public fixed short Lookahead[512];

        /// <summary>
        /// Derives a lookup table for fast AC entropy scan decoding.
        /// This can happen multiple times during progressive decoding but always outside mcu loops.
        /// </summary>
        /// <param name="huffmanTable">The AC Huffman table.</param>
        public void Derive(ref HuffmanTable huffmanTable)
        {
            const int FastBits = ScanDecoder.FastBits;
            ref short fastACRef = ref this.Lookahead[0];
            ref byte huffmanLookaheadRef = ref huffmanTable.Lookahead[0];
            ref byte huffmanValuesRef = ref huffmanTable.Values[0];
            ref short huffmanSizesRef = ref huffmanTable.Sizes[0];

            int i;
            for (i = 0; i < (1 << FastBits); i++)
            {
                byte fast = Unsafe.Add(ref huffmanLookaheadRef, i);
                Unsafe.Add(ref fastACRef, i) = 0;

                if (fast < byte.MaxValue)
                {
                    int rs = Unsafe.Add(ref huffmanValuesRef, fast);
                    int run = (rs >> 4) & 15;
                    int magbits = rs & 15;
                    int len = Unsafe.Add(ref huffmanSizesRef, fast);

                    if (magbits != 0 && len + magbits <= FastBits)
                    {
                        // Magnitude code followed by receive_extend code
                        int k = ((i << len) & ((1 << FastBits) - 1)) >> (FastBits - magbits);
                        int m = 1 << (magbits - 1);
                        if (k < m)
                        {
                            k += (int)((~0U << magbits) + 1);
                        }

                        // If the result is small enough, we can fit it in fastAC table
                        if (k >= -128 && k <= 127)
                        {
                            Unsafe.Add(ref fastACRef, i) = (short)((k << 8) + (run << 4) + (len + magbits));
                        }
                    }
                }
            }
        }
    }
}