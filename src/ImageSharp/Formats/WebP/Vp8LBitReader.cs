// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// A bit reader for VP8L streams.
    /// </summary>
    internal class Vp8LBitReader : BitReaderBase
    {
        /// <summary>
        /// Maximum number of bits (inclusive) the bit-reader can handle.
        /// </summary>
        private const int Vp8LMaxNumBitRead = 24;

        /// <summary>
        /// Number of bits prefetched.
        /// </summary>
        private const int Vp8LLbits = 64;

        /// <summary>
        /// Minimum number of bytes ready after VP8LFillBitWindow.
        /// </summary>
        private const int Vp8LWbits = 32;

        private readonly uint[] BitMask =
        {
            0,
            0x000001, 0x000003, 0x000007, 0x00000f,
            0x00001f, 0x00003f, 0x00007f, 0x0000ff,
            0x0001ff, 0x0003ff, 0x0007ff, 0x000fff,
            0x001fff, 0x003fff, 0x007fff, 0x00ffff,
            0x01ffff, 0x03ffff, 0x07ffff, 0x0fffff,
            0x1fffff, 0x3fffff, 0x7fffff, 0xffffff
        };

        /// <summary>
        /// Pre-fetched bits.
        /// </summary>
        private ulong value;

        /// <summary>
        /// Buffer length.
        /// </summary>
        private readonly long len;

        /// <summary>
        /// Byte position in buffer.
        /// </summary>
        private long pos;

        /// <summary>
        /// Current bit-reading position in value.
        /// </summary>
        private int bitPos;

        /// <summary>
        /// True if a bit was read past the end of buffer.
        /// </summary>
        private bool eos;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LBitReader"/> class.
        /// </summary>
        /// <param name="inputStream">The input stream to read from.</param>
        /// <param name="imageDataSize">The raw image data size in bytes.</param>
        /// <param name="memoryAllocator">Used for allocating memory during reading data from the stream.</param>
        public Vp8LBitReader(Stream inputStream, uint imageDataSize, MemoryAllocator memoryAllocator)
        {
            long length = imageDataSize;

            this.ReadImageDataFromStream(inputStream, (int)imageDataSize, memoryAllocator);

            this.len = length;
            this.value = 0;
            this.bitPos = 0;
            this.eos = false;

            if (length > sizeof(long))
            {
                length = sizeof(long);
            }

            ulong currentValue = 0;
            for (int i = 0; i < length; ++i)
            {
                currentValue |= (ulong)this.Data[i] << (8 * i);
            }

            this.value = currentValue;
            this.pos = length;
        }

        /// <summary>
        /// Reads a unsigned short value from the buffer. The bits of each byte are read in least-significant-bit-first order.
        /// </summary>
        /// <param name="nBits">The number of bits to read (should not exceed 16).</param>
        /// <returns>A ushort value.</returns>
        public uint ReadValue(int nBits)
        {
            Guard.MustBeGreaterThan(nBits, 0, nameof(nBits));

            if (!this.eos && nBits <= Vp8LMaxNumBitRead)
            {
                ulong val = this.PrefetchBits() & this.BitMask[nBits];
                int newBits = this.bitPos + nBits;
                this.bitPos = newBits;
                this.ShiftBytes();
                return (uint)val;
            }

            this.SetEndOfStream();
            return 0;
        }

        /// <summary>
        /// Reads a single bit from the stream.
        /// </summary>
        /// <returns>True if the bit read was 1, false otherwise.</returns>
        public bool ReadBit()
        {
            uint bit = this.ReadValue(1);
            return bit != 0;
        }

        /// <summary>
        /// For jumping over a number of bits in the bit stream when accessed with PrefetchBits and FillBitWindow.
        /// </summary>
        /// <param name="numberOfBits">The number of bits to advance the position.</param>
        public void AdvanceBitPosition(int numberOfBits)
        {
            this.bitPos += numberOfBits;
        }

        /// <summary>
        /// Return the pre-fetched bits, so they can be looked up.
        /// </summary>
        /// <returns>Prefetched bits.</returns>
        public ulong PrefetchBits()
        {
            return this.value >> (this.bitPos & (Vp8LLbits - 1));
        }

        /// <summary>
        /// Advances the read buffer by 4 bytes to make room for reading next 32 bits.
        /// </summary>
        public void FillBitWindow()
        {
            if (this.bitPos >= Vp8LWbits)
            {
                this.DoFillBitWindow();
            }
        }

        /// <summary>
        /// Returns true if there was an attempt at reading bit past the end of the buffer.
        /// </summary>
        /// <returns>True, if end of buffer was reached.</returns>
        public bool IsEndOfStream()
        {
            return this.eos || ((this.pos == this.len) && (this.bitPos > Vp8LLbits));
        }

        private void DoFillBitWindow()
        {
            this.ShiftBytes();
        }

        /// <summary>
        /// If not at EOS, reload up to Vp8LLbits byte-by-byte.
        /// </summary>
        private void ShiftBytes()
        {
            while (this.bitPos >= 8 && this.pos < this.len)
            {
                this.value >>= 8;
                this.value |= (ulong)this.Data[this.pos] << (Vp8LLbits - 8);
                ++this.pos;
                this.bitPos -= 8;
            }

            if (this.IsEndOfStream())
            {
                this.SetEndOfStream();
            }
        }

        private void SetEndOfStream()
        {
            this.eos = true;
            this.bitPos = 0; // To avoid undefined behaviour with shifts.
        }
    }
}
