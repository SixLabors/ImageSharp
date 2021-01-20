// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;

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
        public abstract void WriteEncodedImageToStream(Stream stream);

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
            Span<byte> buffer = stackalloc byte[4];

            stream.Write(WebpConstants.RiffFourCc);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, riffSize);
            stream.Write(buffer);
            stream.Write(WebpConstants.WebPHeader);
        }
    }
}
