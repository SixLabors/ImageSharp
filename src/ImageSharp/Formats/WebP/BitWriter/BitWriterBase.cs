// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Experimental.Webp.BitWriter
{
    internal abstract class BitWriterBase
    {
        /// <summary>
        /// Buffer to write to.
        /// </summary>
        private byte[] buffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="BitWriterBase"/> class.
        /// </summary>
        /// <param name="expectedSize">The expected size in bytes.</param>
        protected BitWriterBase(int expectedSize) => this.buffer = new byte[expectedSize];

        /// <summary>
        /// Initializes a new instance of the <see cref="BitWriterBase"/> class.
        /// Used internally for cloning.
        /// </summary>
        private protected BitWriterBase(byte[] buffer) => this.buffer = buffer;

        public byte[] Buffer => this.buffer;

        /// <summary>
        /// Writes the encoded bytes of the image to the stream. Call Finish() before this.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        public void WriteToStream(Stream stream) => stream.Write(this.Buffer.AsSpan(0, this.NumBytes()));

        /// <summary>
        /// Resizes the buffer to write to.
        /// </summary>
        /// <param name="extraSize">The extra size in bytes needed.</param>
        public abstract void BitWriterResize(int extraSize);

        /// <summary>
        /// Returns the number of bytes of the encoded image data.
        /// </summary>
        /// <returns>The number of bytes of the image data.</returns>
        public abstract int NumBytes();

        /// <summary>
        /// Flush leftover bits.
        /// </summary>
        public abstract void Finish();

        /// <summary>
        /// Writes the encoded image to the stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="exifProfile">The exif profile.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        public abstract void WriteEncodedImageToStream(Stream stream, ExifProfile exifProfile, uint width, uint height);

        protected bool ResizeBuffer(int maxBytes, int sizeRequired)
        {
            if (maxBytes > 0 && sizeRequired < maxBytes)
            {
                return true;
            }

            int newSize = (3 * maxBytes) >> 1;
            if (newSize < sizeRequired)
            {
                newSize = sizeRequired;
            }

            // Make new size multiple of 1k.
            newSize = ((newSize >> 10) + 1) << 10;
            Array.Resize(ref this.buffer, newSize);

            return false;
        }

        /// <summary>
        /// Writes the RIFF header to the stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="riffSize">The block length.</param>
        protected void WriteRiffHeader(Stream stream, uint riffSize)
        {
            Span<byte> buf = stackalloc byte[4];
            stream.Write(WebpConstants.RiffFourCc);
            BinaryPrimitives.WriteUInt32LittleEndian(buf, riffSize);
            stream.Write(buf);
            stream.Write(WebpConstants.WebPHeader);
        }

        /// <summary>
        /// Writes the Exif profile to the stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="exifBytes">The exif profile bytes.</param>
        protected void WriteExifProfile(Stream stream, byte[] exifBytes)
        {
            DebugGuard.NotNull(exifBytes, nameof(exifBytes));

            Span<byte> buf = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)WebpChunkType.Exif);
            stream.Write(buf);
            BinaryPrimitives.WriteUInt32LittleEndian(buf, (uint)exifBytes.Length);
            stream.Write(buf);
            stream.Write(exifBytes);
        }

        /// <summary>
        /// Writes a VP8X header to the stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="exifProfile">A exif profile or null, if it does not exist.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        protected void WriteVp8XHeader(Stream stream, ExifProfile exifProfile, uint width, uint height)
        {
            int maxDimension = 16777215;
            if (width > maxDimension || height > maxDimension)
            {
                WebpThrowHelper.ThrowInvalidImageDimensions($"Image width or height exceeds maximum allowed dimension of {maxDimension}");
            }

            // The spec states that the product of Canvas Width and Canvas Height MUST be at most 2^32 - 1.
            if (width * height > 4294967295ul)
            {
                WebpThrowHelper.ThrowInvalidImageDimensions("The product of image width and height MUST be at most 2^32 - 1");
            }

            uint flags = 0;
            if (exifProfile != null)
            {
                // Set exif bit.
                flags |= 8;
            }

            Span<byte> buf = stackalloc byte[4];
            stream.Write(WebpConstants.Vp8XMagicBytes);
            BinaryPrimitives.WriteUInt32LittleEndian(buf, WebpConstants.Vp8XChunkSize);
            stream.Write(buf);
            BinaryPrimitives.WriteUInt32LittleEndian(buf, flags);
            stream.Write(buf);
            BinaryPrimitives.WriteUInt32LittleEndian(buf, width - 1);
            stream.Write(buf.Slice(0, 3));
            BinaryPrimitives.WriteUInt32LittleEndian(buf, height - 1);
            stream.Write(buf.Slice(0, 3));
        }
    }
}
