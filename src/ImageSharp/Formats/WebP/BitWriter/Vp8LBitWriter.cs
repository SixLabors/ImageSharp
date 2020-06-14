// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.WebP.Lossless;

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
            this.end = this.buffer.Length;
        }

        /// <summary>
        /// This function writes bits into bytes in increasing addresses (little endian),
        /// and within a byte least-significant-bit first. This function can write up to 32 bits in one go.
        /// </summary>
        public void PutBits(uint bits, int nBits)
        {
            if (nBits > 0)
            {
                if (this.used >= 32)
                {
                    this.PutBitsFlushBits();
                }

                this.bits |= (ulong)bits << this.used;
                this.used += nBits;
            }
        }

        public void WriteHuffmanCode(HuffmanTreeCode code, int codeIndex)
        {
            int depth = code.CodeLengths[codeIndex];
            int symbol = code.Codes[codeIndex];
            this.PutBits((uint)symbol, depth);
        }

        public void WriteHuffmanCodeWithExtraBits(HuffmanTreeCode code, int codeIndex, int bits, int nBits)
        {
            int depth = code.CodeLengths[codeIndex];
            int symbol = code.Codes[codeIndex];
            this.PutBits((uint)((bits << depth) | symbol), depth + nBits);
        }

        public int NumBytes()
        {
            return this.cur + ((this.used + 7) >> 3);
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

            BinaryPrimitives.WriteUInt64LittleEndian(this.buffer.AsSpan(this.cur), this.bits);
            this.cur += WriterBytes;
            this.bits >>= WriterBits;
            this.used -= WriterBits;
        }

        private bool BitWriterResize(int extraSize)
        {
            // TODO: resize buffer
            return true;
        }
    }
}
