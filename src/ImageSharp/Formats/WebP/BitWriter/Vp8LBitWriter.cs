// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LBitWriter"/> class.
        /// </summary>
        /// <param name="expectedSize">The expected size in bytes.</param>
        public Vp8LBitWriter(int expectedSize)
        {
            // TODO: maybe use memory allocator here.
            this.buffer = new byte[expectedSize];
            this.end = this.buffer.Length;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LBitWriter"/> class.
        /// Used internally for cloning.
        /// </summary>
        private Vp8LBitWriter(byte[] buffer, ulong bits, int used, int cur)
        {
            this.buffer = buffer;
            this.bits = bits;
            this.used = used;
            this.cur = cur;
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

        public void Reset(Vp8LBitWriter bwInit)
        {
            this.bits = bwInit.bits;
            this.used = bwInit.used;
            this.cur = bwInit.cur;
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

        public Vp8LBitWriter Clone()
        {
            var clonedBuffer = new byte[this.buffer.Length];
            Buffer.BlockCopy(this.buffer, 0, clonedBuffer, 0, this.cur);
            return new Vp8LBitWriter(clonedBuffer, this.bits, this.used, this.cur);
        }

        /// <summary>
        /// Writes the encoded bytes of the image to the stream. Call BitWriterFinish() before this.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        public void WriteToStream(Stream stream)
        {
            stream.Write(this.buffer.AsSpan(0, this.NumBytes()));
        }

        /// <summary>
        /// Flush leftover bits.
        /// </summary>
        public void BitWriterFinish()
        {
            this.BitWriterResize((this.used + 7) >> 3);
            while (this.used > 0)
            {
                this.buffer[this.cur++] = (byte)this.bits;
                this.bits >>= 8;
                this.used -= 8;
            }

            this.used = 0;
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
                this.BitWriterResize(extraSize);
            }

            BinaryPrimitives.WriteUInt64LittleEndian(this.buffer.AsSpan(this.cur), this.bits);
            this.cur += WriterBytes;
            this.bits >>= WriterBits;
            this.used -= WriterBits;
        }

        /// <summary>
        /// Resizes the buffer to write to.
        /// </summary>
        /// <param name="extraSize">The extra size in bytes needed.</param>
        private void BitWriterResize(int extraSize)
        {
            int maxBytes = this.end + this.buffer.Length;
            int sizeRequired = this.cur + extraSize;

            if (maxBytes > 0 && sizeRequired < maxBytes)
            {
                return;
            }

            int newSize = (3 * maxBytes) >> 1;
            if (newSize < sizeRequired)
            {
                newSize = sizeRequired;
            }

            // make new size multiple of 1k
            newSize = ((newSize >> 10) + 1) << 10;
            if (this.cur > 0)
            {
                Array.Resize(ref this.buffer, newSize);
            }

            this.end = this.buffer.Length;
        }
    }
}
