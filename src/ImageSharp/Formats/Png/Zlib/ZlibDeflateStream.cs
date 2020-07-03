// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Png.Zlib
{
    /// <summary>
    /// Provides methods and properties for compressing streams by using the Zlib Deflate algorithm.
    /// </summary>
    internal sealed class ZlibDeflateStream : Stream
    {
        /// <summary>
        /// The raw stream containing the uncompressed image data.
        /// </summary>
        private readonly Stream rawStream;

        /// <summary>
        /// Computes the checksum for the data stream.
        /// </summary>
        private uint adler = Adler32.SeedValue;

        /// <summary>
        /// A value indicating whether this instance of the given entity has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if this instance has been disposed; otherwise, <see langword="false"/>.</value>
        /// <remarks>
        /// If the entity is disposed, it must not be disposed a second
        /// time. The isDisposed field is set the first time the entity
        /// is disposed. If the isDisposed field is true, then the Dispose()
        /// method will not dispose again. This help not to prolong the entity's
        /// life in the Garbage Collector.
        /// </remarks>
        private bool isDisposed;

        /// <summary>
        /// The stream responsible for compressing the input stream.
        /// </summary>
        // private DeflateStream deflateStream;
        private DeflaterOutputStream deflateStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZlibDeflateStream"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator to use for buffer allocations.</param>
        /// <param name="stream">The stream to compress.</param>
        /// <param name="level">The compression level.</param>
        public ZlibDeflateStream(MemoryAllocator memoryAllocator, Stream stream, PngCompressionLevel level)
        {
            int compressionLevel = (int)level;
            this.rawStream = stream;

            // Write the zlib header : http://tools.ietf.org/html/rfc1950
            // CMF(Compression Method and flags)
            // This byte is divided into a 4 - bit compression method and a
            // 4-bit information field depending on the compression method.
            // bits 0 to 3  CM Compression method
            // bits 4 to 7  CINFO Compression info
            //
            //   0   1
            // +---+---+
            // |CMF|FLG|
            // +---+---+
            const int Cmf = 0x78;
            int flg = 218;

            // http://stackoverflow.com/a/2331025/277304
            if (compressionLevel >= 5 && compressionLevel <= 6)
            {
                flg = 156;
            }
            else if (compressionLevel >= 3 && compressionLevel <= 4)
            {
                flg = 94;
            }
            else if (compressionLevel <= 2)
            {
                flg = 1;
            }

            // Just in case
            flg -= ((Cmf * 256) + flg) % 31;

            if (flg < 0)
            {
                flg += 31;
            }

            this.rawStream.WriteByte(Cmf);
            this.rawStream.WriteByte((byte)flg);

            this.deflateStream = new DeflaterOutputStream(memoryAllocator, this.rawStream, compressionLevel);
        }

        /// <inheritdoc/>
        public override bool CanRead => false;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => this.rawStream.CanWrite;

        /// <inheritdoc/>
        public override long Length => this.rawStream.Length;

        /// <inheritdoc/>
        public override long Position
        {
            get
            {
                return this.rawStream.Position;
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        /// <inheritdoc/>
        public override void Flush() => this.deflateStream.Flush();

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc/>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.deflateStream.Write(buffer, offset, count);
            this.adler = Adler32.Calculate(this.adler, buffer.AsSpan(offset, count));
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                // dispose managed resources
                this.deflateStream.Dispose();

                // Add the crc
                uint crc = this.adler;
                this.rawStream.WriteByte((byte)((crc >> 24) & 0xFF));
                this.rawStream.WriteByte((byte)((crc >> 16) & 0xFF));
                this.rawStream.WriteByte((byte)((crc >> 8) & 0xFF));
                this.rawStream.WriteByte((byte)(crc & 0xFF));
            }

            this.deflateStream = null;

            base.Dispose(disposing);
            this.isDisposed = true;
        }
    }
}
