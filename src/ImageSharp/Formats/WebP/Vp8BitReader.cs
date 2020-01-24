// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;

using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// A bit reader for VP8 streams.
    /// </summary>
    internal class Vp8BitReader : BitReaderBase
    {
        private const int BitsCount = 56;

        /// <summary>
        /// Current value.
        /// </summary>
        private ulong value;

        /// <summary>
        /// Current range minus 1. In [127, 254] interval.
        /// </summary>
        private uint range;

        /// <summary>
        /// Number of valid bits left.
        /// </summary>
        private int bits;

        /// <summary>
        /// The next byte to be read.
        /// </summary>
        private byte buf;

        /// <summary>
        /// True if input is exhausted.
        /// </summary>
        private bool eof;

        /// <summary>
        /// Byte position in buffer.
        /// </summary>
        private long pos;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8BitReader"/> class.
        /// </summary>
        /// <param name="inputStream">The input stream to read from.</param>
        /// <param name="imageDataSize">The raw image data size in bytes.</param>
        /// <param name="memoryAllocator">Used for allocating memory during reading data from the stream.</param>
        public Vp8BitReader(Stream inputStream, uint imageDataSize, MemoryAllocator memoryAllocator)
        {
            this.ReadImageDataFromStream(inputStream, (int)imageDataSize, memoryAllocator);

            this.range = 255 - 1;
            this.value = 0;
            this.bits = -8; // to load the very first 8bits.
            this.eof = false;
            this.pos = 0;

            this.LoadNewBytes();
        }

        public int GetBit(int prob)
        {
            Guard.MustBeGreaterThan(prob, 0, nameof(prob));

            uint range = this.range;
            if (this.bits < 0)
            {
                this.LoadNewBytes();
            }

            int pos = this.bits;
            uint split = (uint)((range * prob) >> 8);
            ulong value = this.value >> pos;
            bool bit = value > split;
            if (bit)
            {
                range -= split;
                this.value -= (ulong)(split + 1) << pos;
            }
            else
            {
                range = split + 1;
            }

            int shift = 7 ^ this.BitsLog2Floor(range);
            range <<= shift;
            this.bits -= shift;

            this.range = range - 1;

            return bit ? 1 : 0;
        }

        public bool ReadBool()
        {
            return this.ReadValue(1) is 1;
        }

        public uint ReadValue(int nBits)
        {
            Guard.MustBeGreaterThan(nBits, 0, nameof(nBits));

            uint v = 0;
            while (nBits-- > 0)
            {
                v |= (uint)this.GetBit(0x80) << nBits;
            }

            return v;
        }

        public int ReadSignedValue(int nBits)
        {
            Guard.MustBeGreaterThan(nBits, 0, nameof(nBits));

            int value = (int)this.ReadValue(nBits);
            return this.ReadValue(1) != 0 ? -value : value;
        }

        private void LoadNewBytes()
        {
            if (this.pos < this.Data.Length)
            {
                ulong bits;
                ulong inBits = BinaryPrimitives.ReadUInt64LittleEndian(this.Data.AsSpan().Slice((int)this.pos, 8));
                this.pos += BitsCount >> 3;
                this.buf = this.Data[this.pos];
                bits = this.ByteSwap64(inBits);
                bits >>= 64 - BitsCount;
                this.value = bits | (this.value << BitsCount);
                this.bits += BitsCount;
            }
            else
            {
                this.LoadFinalBytes();
            }
        }

        private void LoadFinalBytes()
        {
            // Only read 8bits at a time.
            if (this.pos < this.Data.Length)
            {
                this.bits += 8;
                this.value = this.Data[this.pos++] | (this.value << 8);
            }
            else if (!this.eof)
            {
                this.value <<= 8;
                this.bits += 8;
                this.eof = true;
            }
            else
            {
                this.bits = 0;  // This is to avoid undefined behaviour with shifts.
            }
        }

        private ulong ByteSwap64(ulong x)
        {
            x = ((x & 0xffffffff00000000ul) >> 32) | ((x & 0x00000000fffffffful) << 32);
            x = ((x & 0xffff0000ffff0000ul) >> 16) | ((x & 0x0000ffff0000fffful) << 16);
            x = ((x & 0xff00ff00ff00ff00ul) >> 8) | ((x & 0x00ff00ff00ff00fful) << 8);
            return x;
        }

        private int BitsLog2Floor(uint n)
        {
            // Search the mask data from most significant bit (MSB) to least significant bit (LSB) for a set bit (1).
            // https://docs.microsoft.com/en-us/cpp/intrinsics/bitscanreverse-bitscanreverse64?view=vs-2019
            uint mask = 1;
            for (int y = 0; y < 32; y++)
            {
                if ((mask & n) == mask)
                {
                    return y;
                }

                mask <<= 1;
            }

            return 0;
        }
    }
}
