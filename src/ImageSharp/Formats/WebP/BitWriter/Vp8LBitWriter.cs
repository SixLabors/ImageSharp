// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;

namespace SixLabors.ImageSharp.Formats.WebP.BitWriter
{
    /// <summary>
    /// A bit writer for writing lossless webp streams.
    /// </summary>
    internal class Vp8LBitWriter
    {
        /// <summary>
        /// This is the minimum amount of size the memory buffer is guaranteed to grow when extra space is needed.
        /// </summary>
        private const int MinExtraSize = 32768;

        private const int WriterBytes = 4;

        private const int WriterBits = 32;

        private const int WriterMaxBits = 64;

        /// <summary>
        /// Bit accumulator.
        /// </summary>
        private ulong bits;

        /// <summary>
        /// Number of bits used in accumulator.
        /// </summary>
        private int used;

        /// <summary>
        /// Buffer to write to.
        /// </summary>
        private byte[] buffer;

        /// <summary>
        /// Current write position.
        /// </summary>
        private int cur;

        private int end;

        private bool error;

        public Vp8LBitWriter(int expectedSize)
        {
            this.buffer = new byte[expectedSize];
        }

        /// <summary>
        /// This function writes bits into bytes in increasing addresses (little endian),
        /// and within a byte least-significant-bit first.
        /// This function can write up to 32 bits in one go, but VP8LBitReader can only
        /// read 24 bits max (VP8L_MAX_NUM_BIT_READ).
        /// </summary>
        public void PutBits(uint bits, int nBits)
        {
            if (nBits > 0)
            {
                if (this.used >= 32)
                {
                    this.PutBitsFlushBits();
                }

                this.bits |= bits << this.used;
                this.used += nBits;
            }
        }

        /// <summary>
        /// Internal function for PutBits flushing 32 bits from the written state.
        /// </summary>
        private void PutBitsFlushBits()
        {
            // If needed, make some room by flushing some bits out.
            if (this.cur + WriterBytes > this.end)
            {
                var extraSize = (this.end - this.cur) + MinExtraSize;
                if (!this.BitWriterResize(extraSize))
                {
                    this.error = true;
                    return;
                }
            }

            //*(vp8l_wtype_t*)bw->cur_ = (vp8l_wtype_t)WSWAP((vp8l_wtype_t)bw->bits_);
            this.cur += WriterBytes;
            this.bits >>= WriterBits;
            this.used -= WriterBits;
        }

        private bool BitWriterResize(int extraSize)
        {
            return true;
        }
    }
}
