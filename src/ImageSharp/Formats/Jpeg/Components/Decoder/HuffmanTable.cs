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
        public fixed uint MaxCode[18];

        /// <summary>
        /// Gets the value offset array
        /// </summary>
        public fixed int ValOffset[18];

        /// <summary>
        /// Gets the huffman value array
        /// </summary>
        public fixed byte Values[256];

        /// <summary>
        /// Gets the lookahead array
        /// </summary>
        public fixed byte Lookahead[512];

        /// <summary>
        /// Gets the sizes array
        /// </summary>
        public fixed short Sizes[257];

        /// <summary>
        /// Initializes a new instance of the <see cref="HuffmanTable"/> struct.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations.</param>
        /// <param name="codeLengths">The code lengths</param>
        /// <param name="values">The huffman values</param>
        public HuffmanTable(MemoryAllocator memoryAllocator, ReadOnlySpan<byte> codeLengths, ReadOnlySpan<byte> values)
        {
            // We do some bounds checks in the code here to protect against AccessViolationExceptions
            const int HuffCodeLength = 257;
            const int MaxSizeLength = HuffCodeLength - 1;
            using (IMemoryOwner<short> huffcode = memoryAllocator.Allocate<short>(HuffCodeLength))
            {
                ref short huffcodeRef = ref MemoryMarshal.GetReference(huffcode.GetSpan());
                ref byte codeLengthsRef = ref MemoryMarshal.GetReference(codeLengths);

                // Figure C.1: make table of Huffman code length for each symbol
                ref short sizesRef = ref this.Sizes[0];
                short x = 0;

                for (short i = 1; i < 17; i++)
                {
                    byte length = Unsafe.Add(ref codeLengthsRef, i);
                    for (short j = 0; j < length && x < MaxSizeLength; j++)
                    {
                        Unsafe.Add(ref sizesRef, x++) = i;
                    }
                }

                Unsafe.Add(ref sizesRef, x) = 0;

                // Figure C.2: generate the codes themselves
                int si = 0;
                ref int valOffsetRef = ref this.ValOffset[0];
                ref uint maxcodeRef = ref this.MaxCode[0];

                uint code = 0;
                int k;
                for (k = 1; k < 17; k++)
                {
                    // Compute delta to add to code to compute symbol id.
                    Unsafe.Add(ref valOffsetRef, k) = (int)(si - code);
                    if (Unsafe.Add(ref sizesRef, si) == k)
                    {
                        while (Unsafe.Add(ref sizesRef, si) == k && si < HuffCodeLength)
                        {
                            Unsafe.Add(ref huffcodeRef, si++) = (short)code++;
                        }
                    }

                    // Figure F.15: generate decoding tables for bit-sequential decoding.
                    // Compute largest code + 1 for this size. preshifted as we need it later.
                    Unsafe.Add(ref maxcodeRef, k) = code << (16 - k);
                    code <<= 1;
                }

                Unsafe.Add(ref maxcodeRef, k) = 0xFFFFFFFF;

                // Generate non-spec lookup tables to speed up decoding.
                const int FastBits = ScanDecoder.FastBits;
                ref byte lookaheadRef = ref this.Lookahead[0];
                const uint MaxFastLength = 1 << FastBits;
                Unsafe.InitBlockUnaligned(ref lookaheadRef, 0xFF, MaxFastLength); // Flag for non-accelerated

                for (int i = 0; i < si; i++)
                {
                    int size = Unsafe.Add(ref sizesRef, i);
                    if (size <= FastBits)
                    {
                        int huffCode = Unsafe.Add(ref huffcodeRef, i) << (FastBits - size);
                        int max = 1 << (FastBits - size);
                        for (int left = 0; left < max; left++)
                        {
                            Unsafe.Add(ref lookaheadRef, huffCode + left) = (byte)i;
                        }
                    }
                }
            }

            Unsafe.CopyBlockUnaligned(ref this.Values[0], ref MemoryMarshal.GetReference(values), 256);
        }
    }
}