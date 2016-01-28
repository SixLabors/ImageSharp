// <copyright file="ZlibInflateStream.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;
    using System.IO.Compression;

    /// <summary>
    /// Provides methods and properties for decompressing streams by using the Zlib Deflate algorithm.
    /// </summary>
    internal sealed class ZlibInflateStream : Stream
    {
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
        /// The raw stream containing the uncompressed image data.
        /// </summary>
        private readonly Stream rawStream;

        /// <summary>
        /// The preset dictionary. 
        /// Merely informational, not used.
        /// </summary>
        private bool fdict;

        /// <summary>
        /// The DICT dictionary identifier identifying the used dictionary.
        /// Merely informational, not used.
        /// </summary>
        private byte[] dictId;

        /// <summary>
        /// CINFO is the base-2 logarithm of the LZ77 window size, minus eight.
        /// Merely informational, not used.
        /// </summary>
        private int cinfo;

        /// <summary>
        /// The read crc data.
        /// </summary>
        private byte[] crcread;

        // The stream responsible for decompressing the input stream.
        private DeflateStream deflateStream;

        public ZlibInflateStream(Stream stream)
        {
            this.rawStream = stream;

            // Read the zlib header : http://tools.ietf.org/html/rfc1950
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
            int cmf = this.rawStream.ReadByte();
            int flag = this.rawStream.ReadByte();
            if (cmf == -1 || flag == -1)
            {
                return;
            }

            if ((cmf & 0x0f) != 8)
            {
                throw new Exception($"Bad compression method for ZLIB header: cmf={cmf}");
            }

            this.cinfo = ((cmf & (0xf0)) >> 8);
            this.fdict = (flag & 32) != 0;

            if (this.fdict)
            {
                this.dictId = new byte[4];

                for (int i = 0; i < 4; i++)
                {
                    // We consume but don't use this.
                    this.dictId[i] = (byte)this.rawStream.ReadByte(); 
                }
            }

            // Initialize the deflate Stream.
            this.deflateStream = new DeflateStream(this.rawStream, CompressionMode.Decompress, true);
        }

        /// <inheritdoc/>
        public override bool CanRead => true;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => false;

        /// <inheritdoc/>
        public override long Length
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <inheritdoc/>
        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            this.deflateStream?.Flush();
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            // We dont't check CRC on reading
            int read = this.deflateStream.Read(buffer, offset, count);
            if (read < 1 && this.crcread == null)
            {
                // The deflater has ended. We try to read the next 4 bytes from raw stream (crc)
                this.crcread = new byte[4];
                for (int i = 0; i < 4; i++)
                {
                    // we dont really check/use this
                    this.crcread[i] = (byte)this.rawStream.ReadByte();
                } 
            }

            return read;
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
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
                if (this.deflateStream != null)
                {
                    this.deflateStream.Dispose();
                    this.deflateStream = null;

                    if (this.crcread == null)
                    {
                        // Consume the trailing 4 bytes
                        this.crcread = new byte[4];
                        for (int i = 0; i < 4; i++)
                        {
                            this.crcread[i] = (byte)this.rawStream.ReadByte();
                        }
                    }
                }
            }

            base.Dispose(disposing);

            // Call the appropriate methods to clean up
            // unmanaged resources here.
            // Note disposing is done.
            this.isDisposed = true;
        }
    }
}
