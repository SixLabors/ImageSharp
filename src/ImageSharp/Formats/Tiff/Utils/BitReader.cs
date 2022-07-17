// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Tiff.Utils
{
    /// <summary>
    /// Utility class to read a sequence of bits from an array
    /// </summary>
    internal ref struct BitReader
    {
        private readonly ReadOnlySpan<byte> array;
        private int offset;
        private int bitOffset;

        /// <summary>
        /// Initializes a new instance of the <see cref="BitReader" /> struct.
        /// </summary>
        /// <param name="array">The array to read data from.</param>
        public BitReader(ReadOnlySpan<byte> array)
        {
            this.array = array;
            this.offset = 0;
            this.bitOffset = 0;
        }

        /// <summary>
        /// Reads the specified number of bits from the array.
        /// </summary>
        /// <param name="bits">The number of bits to read.</param>
        /// <returns>The value read from the array.</returns>
        public int ReadBits(uint bits)
        {
            int value = 0;

            for (uint i = 0; i < bits; i++)
            {
                int bit = (this.array[this.offset] >> (7 - this.bitOffset)) & 0x01;
                value = (value << 1) | bit;

                this.bitOffset++;

                if (this.bitOffset == 8)
                {
                    this.bitOffset = 0;
                    this.offset++;
                }
            }

            return value;
        }

        /// <summary>
        /// Moves the reader to the next row of byte-aligned data.
        /// </summary>
        public void NextRow()
        {
            if (this.bitOffset > 0)
            {
                this.bitOffset = 0;
                this.offset++;
            }
        }
    }
}
