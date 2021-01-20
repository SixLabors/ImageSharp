// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using SixLabors.ImageSharp.Formats.Experimental.Webp.Lossless;

namespace SixLabors.ImageSharp.Formats.Experimental.Webp.BitWriter
{
    /// <summary>
    /// A bit writer for writing lossless webp streams.
    /// </summary>
    internal class Vp8LBitWriter : BitWriterBase
    {
        /// <summary>
        /// A scratch buffer to reduce allocations.
        /// </summary>
        private readonly byte[] scratchBuffer = new byte[8];

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
        /// Current write position.
        /// </summary>
        private int cur;

        private int end;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LBitWriter"/> class.
        /// </summary>
        /// <param name="expectedSize">The expected size in bytes.</param>
        public Vp8LBitWriter(int expectedSize)
            : base(expectedSize) => this.end = this.Buffer.Length;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LBitWriter"/> class.
        /// Used internally for cloning.
        /// </summary>
        private Vp8LBitWriter(byte[] buffer, ulong bits, int used, int cur)
            : base(buffer)
        {
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

        /// <inheritdoc/>
        public override int NumBytes() => this.cur + ((this.used + 7) >> 3);

        public Vp8LBitWriter Clone()
        {
            var clonedBuffer = new byte[this.Buffer.Length];
            System.Buffer.BlockCopy(this.Buffer, 0, clonedBuffer, 0, this.cur);
            return new Vp8LBitWriter(clonedBuffer, this.bits, this.used, this.cur);
        }

        /// <inheritdoc/>
        public override void Finish()
        {
            this.BitWriterResize((this.used + 7) >> 3);
            while (this.used > 0)
            {
                this.Buffer[this.cur++] = (byte)this.bits;
                this.bits >>= 8;
                this.used -= 8;
            }

            this.used = 0;
        }

        /// <inheritdoc/>
        public override void WriteEncodedImageToStream(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[4];

            this.Finish();
            uint size = (uint)this.NumBytes();
            size++; // One byte extra for the VP8L signature.

            // Write RIFF header.
            uint pad = size & 1;
            uint riffSize = WebpConstants.TagSize + WebpConstants.ChunkHeaderSize + size + pad;
            this.WriteRiffHeader(stream, riffSize);
            stream.Write(WebpConstants.Vp8LMagicBytes);

            // Write Vp8 Header.
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, size);
            stream.Write(buffer);
            stream.WriteByte(WebpConstants.Vp8LHeaderMagicByte);

            this.WriteToStream(stream);
            if (pad == 1)
            {
                stream.WriteByte(0);
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
                var extraSize = this.end - this.cur + MinExtraSize;
                this.BitWriterResize(extraSize);
            }

            BinaryPrimitives.WriteUInt64LittleEndian(this.scratchBuffer, this.bits);
            this.scratchBuffer.AsSpan(0, 4).CopyTo(this.Buffer.AsSpan(this.cur));

            this.cur += WriterBytes;
            this.bits >>= WriterBits;
            this.used -= WriterBits;
        }

        /// <summary>
        /// Resizes the buffer to write to.
        /// </summary>
        /// <param name="extraSize">The extra size in bytes needed.</param>
        public override void BitWriterResize(int extraSize)
        {
            int maxBytes = this.end + this.Buffer.Length;
            int sizeRequired = this.cur + extraSize;

            if (this.ResizeBuffer(maxBytes, sizeRequired))
            {
                return;
            }

            this.end = this.Buffer.Length;
        }
    }
}
