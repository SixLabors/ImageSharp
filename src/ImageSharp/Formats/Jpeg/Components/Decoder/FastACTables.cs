// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// The collection of lookup tables used for fast AC entropy scan decoding.
    /// </summary>
    internal sealed class FastACTables : IDisposable
    {
        private Buffer2D<short> tables;

        /// <summary>
        /// Initializes a new instance of the <see cref="FastACTables"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator used to allocate memory for image processing operations.</param>
        public FastACTables(MemoryAllocator memoryAllocator) => this.tables = memoryAllocator.Allocate2D<short>(512, 4, AllocationOptions.Clean);

        /// <summary>
        /// Gets the <see cref="Span{Int16}"/> representing the table at the index in the collection.
        /// </summary>
        /// <param name="index">The table index.</param>
        /// <returns><see cref="Span{Int16}"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public ReadOnlySpan<short> GetTableSpan(int index) => this.tables.GetRowSpan(index);

        /// <summary>
        /// Gets a reference to the first element of the AC table indexed by <see cref="JpegComponent.ACHuffmanTableId"/>
        /// </summary>
        /// <param name="component">The frame component.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public ref short GetAcTableReference(JpegComponent component) => ref this.tables.GetRowSpan(component.ACHuffmanTableId)[0];

        /// <summary>
        /// Builds a lookup table for fast AC entropy scan decoding.
        /// </summary>
        /// <param name="index">The table index.</param>
        /// <param name="acHuffmanTables">The collection of AC Huffman tables.</param>
        public unsafe void BuildACTableLut(int index, HuffmanTables acHuffmanTables)
        {
            const int FastBits = ScanDecoder.FastBits;
            Span<short> fastAC = this.tables.GetRowSpan(index);
            ref HuffmanTable huffmanTable = ref acHuffmanTables[index];

            int i;
            for (i = 0; i < (1 << FastBits); i++)
            {
                byte fast = huffmanTable.Lookahead[i];
                fastAC[i] = 0;
                if (fast < byte.MaxValue)
                {
                    int rs = huffmanTable.Values[fast];
                    int run = (rs >> 4) & 15;
                    int magbits = rs & 15;
                    int len = huffmanTable.Sizes[fast];

                    if (magbits != 0 && len + magbits <= FastBits)
                    {
                        // Magnitude code followed by receive_extend code
                        int k = ((i << len) & ((1 << FastBits) - 1)) >> (FastBits - magbits);
                        int m = 1 << (magbits - 1);
                        if (k < m)
                        {
                            k += (int)((~0U << magbits) + 1);
                        }

                        // if the result is small enough, we can fit it in fastAC table
                        if (k >= -128 && k <= 127)
                        {
                            fastAC[i] = (short)((k << 8) + (run << 4) + (len + magbits));
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.tables?.Dispose();
            this.tables = null;
        }
    }
}