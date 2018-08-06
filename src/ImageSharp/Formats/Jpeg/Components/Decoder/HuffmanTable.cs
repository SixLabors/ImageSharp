// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Represents a Huffman Table
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct HuffmanTable
    {
        /// <summary>
        /// Gets the max code array
        /// </summary>
        public FixedUInt32Buffer18 MaxCode;

        /// <summary>
        /// Gets the value offset array
        /// </summary>
        public FixedInt32Buffer18 ValOffset;

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
        /// Initializes a new instance of the <see cref="HuffmanTable"/> struct.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations.</param>
        /// <param name="count">The code lengths</param>
        /// <param name="values">The huffman values</param>
        public HuffmanTable(MemoryAllocator memoryAllocator, ReadOnlySpan<byte> count, ReadOnlySpan<byte> values)
        {
            const int Length = 257;
            using (IMemoryOwner<short> huffcode = memoryAllocator.Allocate<short>(Length))
            {
                ref short huffcodeRef = ref MemoryMarshal.GetReference(huffcode.GetSpan());

                // Figure C.1: make table of Huffman code length for each symbol
                fixed (short* sizesRef = this.Sizes.Data)
                {
                    short x = 0;
                    for (short i = 1; i < 17; i++)
                    {
                        byte l = count[i];
                        for (short j = 0; j < l; j++)
                        {
                            sizesRef[x] = i;
                            x++;
                        }
                    }

                    sizesRef[x] = 0;

                    // Figure C.2: generate the codes themselves
                    int k = 0;
                    fixed (int* valOffsetRef = this.ValOffset.Data)
                    fixed (uint* maxcodeRef = this.MaxCode.Data)
                    {
                        uint code = 0;
                        int j;
                        for (j = 1; j < 17; j++)
                        {
                            // Compute delta to add to code to compute symbol id.
                            valOffsetRef[j] = (int)(k - code);
                            if (sizesRef[k] == j)
                            {
                                while (sizesRef[k] == j)
                                {
                                    Unsafe.Add(ref huffcodeRef, k++) = (short)code++;
                                }
                            }

                            // Figure F.15: generate decoding tables for bit-sequential decoding.
                            // Compute largest code + 1 for this size. preshifted as need later.
                            maxcodeRef[j] = code << (16 - j);
                            code <<= 1;
                        }

                        maxcodeRef[j] = 0xFFFFFFFF;
                    }

                    // Generate non-spec lookup tables to speed up decoding.
                    fixed (byte* lookaheadRef = this.Lookahead.Data)
                    {
                        const int FastBits = ScanDecoder.FastBits;
                        var fast = new Span<byte>(lookaheadRef, 1 << FastBits);
                        fast.Fill(0xFF); // Flag for non-accelerated

                        for (int i = 0; i < k; i++)
                        {
                            int s = sizesRef[i];
                            if (s <= ScanDecoder.FastBits)
                            {
                                int c = Unsafe.Add(ref huffcodeRef, i) << (FastBits - s);
                                int m = 1 << (FastBits - s);
                                for (int j = 0; j < m; j++)
                                {
                                    fast[c + j] = (byte)i;
                                }
                            }
                        }
                    }
                }
            }

            fixed (byte* huffValRef = this.Values.Data)
            {
                var huffValSpan = new Span<byte>(huffValRef, 256);
                values.CopyTo(huffValSpan);
            }
        }
    }
}