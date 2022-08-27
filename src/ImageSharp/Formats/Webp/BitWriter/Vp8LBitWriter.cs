// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers.Binary;
using System.IO;
using SixLabors.ImageSharp.Formats.Webp.Lossless;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;

namespace SixLabors.ImageSharp.Formats.Webp.BitWriter
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

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LBitWriter"/> class.
        /// </summary>
        /// <param name="expectedSize">The expected size in bytes.</param>
        public Vp8LBitWriter(int expectedSize)
            : base(expectedSize)
        {
        }

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
            byte[] clonedBuffer = new byte[this.Buffer.Length];
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

        /// <summary>
        /// Writes the encoded image to the stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="exifProfile">The exif profile.</param>
        /// <param name="xmpProfile">The XMP profile.</param>
        /// <param name="iccProfile">The color profile.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="hasAlpha">Flag indicating, if a alpha channel is present.</param>
        public void WriteEncodedImageToStream(Stream stream, ExifProfile exifProfile, XmpProfile xmpProfile, IccProfile iccProfile, uint width, uint height, bool hasAlpha)
        {
            bool isVp8X = false;
            byte[] exifBytes = null;
            byte[] xmpBytes = null;
            byte[] iccBytes = null;
            uint riffSize = 0;
            if (exifProfile != null)
            {
                isVp8X = true;
                exifBytes = exifProfile.ToByteArray();
                riffSize += this.MetadataChunkSize(exifBytes);
            }

            if (xmpProfile != null)
            {
                isVp8X = true;
                xmpBytes = xmpProfile.Data;
                riffSize += this.MetadataChunkSize(xmpBytes);
            }

            if (iccProfile != null)
            {
                isVp8X = true;
                iccBytes = iccProfile.ToByteArray();
                riffSize += this.MetadataChunkSize(iccBytes);
            }

            if (isVp8X)
            {
                riffSize += ExtendedFileChunkSize;
            }

            this.Finish();
            uint size = (uint)this.NumBytes();
            size++; // One byte extra for the VP8L signature.

            // Write RIFF header.
            uint pad = size & 1;
            riffSize += WebpConstants.TagSize + WebpConstants.ChunkHeaderSize + size + pad;
            this.WriteRiffHeader(stream, riffSize);

            // Write VP8X, header if necessary.
            if (isVp8X)
            {
                this.WriteVp8XHeader(stream, exifProfile, xmpProfile, iccBytes, width, height, hasAlpha);

                if (iccBytes != null)
                {
                    this.WriteColorProfile(stream, iccBytes);
                }
            }

            // Write magic bytes indicating its a lossless webp.
            stream.Write(WebpConstants.Vp8LMagicBytes);

            // Write Vp8 Header.
            BinaryPrimitives.WriteUInt32LittleEndian(this.scratchBuffer, size);
            stream.Write(this.scratchBuffer.AsSpan(0, 4));
            stream.WriteByte(WebpConstants.Vp8LHeaderMagicByte);

            // Write the encoded bytes of the image to the stream.
            this.WriteToStream(stream);
            if (pad == 1)
            {
                stream.WriteByte(0);
            }

            if (exifProfile != null)
            {
                this.WriteMetadataProfile(stream, exifBytes, WebpChunkType.Exif);
            }

            if (xmpProfile != null)
            {
                this.WriteMetadataProfile(stream, xmpBytes, WebpChunkType.Xmp);
            }
        }

        /// <summary>
        /// Internal function for PutBits flushing 32 bits from the written state.
        /// </summary>
        private void PutBitsFlushBits()
        {
            // If needed, make some room by flushing some bits out.
            if (this.cur + WriterBytes > this.Buffer.Length)
            {
                int extraSize = this.Buffer.Length - this.cur + MinExtraSize;
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
            int maxBytes = this.Buffer.Length + this.Buffer.Length;
            int sizeRequired = this.cur + extraSize;
            this.ResizeBuffer(maxBytes, sizeRequired);
        }
    }
}
