// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// A bit reader for VP8 streams.
    /// </summary>
    internal class Vp8LBitReader
    {
        /// <summary>
        /// Maximum number of bits (inclusive) the bit-reader can handle.
        /// </summary>
        private const int VP8L_MAX_NUM_BIT_READ = 24;

        /// <summary>
        /// Number of bits prefetched (= bit-size of vp8l_val_t).
        /// </summary>
        private const int VP8L_LBITS = 64;

        /// <summary>
        /// Minimum number of bytes ready after VP8LFillBitWindow.
        /// </summary>
        private const int VP8L_WBITS = 32;

        private readonly uint[] kBitMask =
        {
            0,
            0x000001, 0x000003, 0x000007, 0x00000f,
            0x00001f, 0x00003f, 0x00007f, 0x0000ff,
            0x0001ff, 0x0003ff, 0x0007ff, 0x000fff,
            0x001fff, 0x003fff, 0x007fff, 0x00ffff,
            0x01ffff, 0x03ffff, 0x07ffff, 0x0fffff,
            0x1fffff, 0x3fffff, 0x7fffff, 0xffffff
        };

        private readonly byte[] data;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LBitReader"/> class.
        /// </summary>
        /// <param name="inputStream">The input stream to read from.</param>
        public Vp8LBitReader(Stream inputStream)
        {
            long length = inputStream.Length - inputStream.Position;

            using (var ms = new MemoryStream())
            {
                inputStream.CopyTo(ms);
                this.data = ms.ToArray();
            }

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
                currentValue |= (ulong)this.data[i] << (8 * i);
            }

            this.value = currentValue;
            this.pos = length;
        }

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
        /// Reads a unsigned short value from the inputStream. The bits of each byte are read in least-significant-bit-first order.
        /// </summary>
        /// <param name="nBits">The number of bits to read (should not exceed 16).</param>
        /// <returns>A ushort value.</returns>
        public uint ReadBits(int nBits)
        {
            Guard.MustBeGreaterThan(nBits, 0, nameof(nBits));

            if (!this.eos && nBits <= VP8L_MAX_NUM_BIT_READ)
            {
                ulong val = this.PrefetchBits() & this.kBitMask[nBits];
                int newBits = this.bitPos + nBits;
                this.bitPos = newBits;
                this.ShiftBytes();
                return (uint)val;
            }
            else
            {
                this.SetEndOfStream();
                return 0;
            }
        }

        public bool ReadBit()
        {
            uint bit = this.ReadBits(1);
            return bit != 0;
        }

        public void AdvanceBitPosition(int bitPosition)
        {
            this.bitPos += bitPosition;
        }

        public ulong PrefetchBits()
        {
            return this.value >> (this.bitPos & (VP8L_LBITS - 1));
        }

        public void FillBitWindow()
        {
            if (this.bitPos >= VP8L_WBITS)
            {
                this.DoFillBitWindow();
            }
        }

        public void DoFillBitWindow()
        {
            this.ShiftBytes();
        }

        public bool IsEndOfStream()
        {
            return this.eos || ((this.pos == this.len) && (this.bitPos > VP8L_LBITS));
        }

        private void ShiftBytes()
        {
            while (this.bitPos >= 8 && this.pos < this.len)
            {
                this.value >>= 8;
                this.value |= (ulong)this.data[this.pos] << (VP8L_LBITS - 8);
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
