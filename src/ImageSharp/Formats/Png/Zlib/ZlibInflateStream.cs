// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.IO.Compression;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Formats.Png.Zlib
{
    /// <summary>
    /// Provides methods and properties for deframing streams from PNGs.
    /// </summary>
    internal sealed class ZlibInflateStream : Stream
    {
        /// <summary>
        /// Used to read the Adler-32 and Crc-32 checksums.
        /// We don't actually use this for anything so it doesn't
        /// have to be threadsafe.
        /// </summary>
        private static readonly byte[] ChecksumBuffer = new byte[4];

        /// <summary>
        /// A default delegate to get more data from the inner stream.
        /// </summary>
        private static readonly Func<int> GetDataNoOp = () => 0;

        /// <summary>
        /// The inner raw memory stream.
        /// </summary>
        private readonly BufferedReadStream innerStream;

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
        /// The current data remaining to be read.
        /// </summary>
        private int currentDataRemaining;

        /// <summary>
        /// Delegate to get more data once we've exhausted the current data remaining.
        /// </summary>
        private readonly Func<int> getData;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZlibInflateStream"/> class.
        /// </summary>
        /// <param name="innerStream">The inner raw stream.</param>
        public ZlibInflateStream(BufferedReadStream innerStream)
            : this(innerStream, GetDataNoOp)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZlibInflateStream"/> class.
        /// </summary>
        /// <param name="innerStream">The inner raw stream.</param>
        /// <param name="getData">A delegate to get more data from the inner stream.</param>
        public ZlibInflateStream(BufferedReadStream innerStream, Func<int> getData)
        {
            this.innerStream = innerStream;
            this.getData = getData;
        }

        /// <inheritdoc/>
        public override bool CanRead => this.innerStream.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        /// <summary>
        /// Gets the compressed stream over the deframed inner stream.
        /// </summary>
        public DeflateStream CompressedStream { get; private set; }

        /// <summary>
        /// Adds new bytes from a frame found in the original stream.
        /// </summary>
        /// <param name="bytes">The current remaining data according to the chunk length.</param>
        /// <param name="isCriticalChunk">Whether the chunk to be inflated is a critical chunk.</param>
        /// <returns>The <see cref="bool"/>.</returns>
        public bool AllocateNewBytes(int bytes, bool isCriticalChunk)
        {
            this.currentDataRemaining = bytes;
            if (this.CompressedStream is null)
            {
                return this.InitializeInflateStream(isCriticalChunk);
            }

            return true;
        }

        /// <inheritdoc/>
        public override void Flush() => throw new NotSupportedException();

        /// <inheritdoc/>
        public override int ReadByte()
        {
            this.currentDataRemaining--;
            return this.innerStream.ReadByte();
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.currentDataRemaining == 0)
            {
                // Last buffer was read in its entirety, let's make sure we don't actually have more in additional IDAT chunks.
                this.currentDataRemaining = this.getData();

                if (this.currentDataRemaining == 0)
                {
                    return 0;
                }
            }

            int bytesToRead = Math.Min(count, this.currentDataRemaining);
            this.currentDataRemaining -= bytesToRead;
            int totalBytesRead = this.innerStream.Read(buffer, offset, bytesToRead);
            long innerStreamLength = this.innerStream.Length;

            // Keep reading data until we've reached the end of the stream or filled the buffer.
            int bytesRead = 0;
            offset += totalBytesRead;
            while (this.currentDataRemaining == 0 && totalBytesRead < count)
            {
                this.currentDataRemaining = this.getData();

                if (this.currentDataRemaining == 0)
                {
                    return totalBytesRead;
                }

                offset += bytesRead;

                if (offset >= innerStreamLength || offset >= count)
                {
                    return totalBytesRead;
                }

                bytesToRead = Math.Min(count - totalBytesRead, this.currentDataRemaining);
                this.currentDataRemaining -= bytesToRead;
                bytesRead = this.innerStream.Read(buffer, offset, bytesToRead);
                totalBytesRead += bytesRead;
            }

            return totalBytesRead;
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
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
                // Dispose managed resources.
                if (this.CompressedStream != null)
                {
                    this.CompressedStream.Dispose();
                    this.CompressedStream = null;
                }
            }

            base.Dispose(disposing);

            // Call the appropriate methods to clean up
            // unmanaged resources here.
            // Note disposing is done.
            this.isDisposed = true;
        }

        private bool InitializeInflateStream(bool isCriticalChunk)
        {
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
            int cmf = this.innerStream.ReadByte();
            int flag = this.innerStream.ReadByte();
            this.currentDataRemaining -= 2;
            if (cmf == -1 || flag == -1)
            {
                return false;
            }

            if ((cmf & 0x0F) == 8)
            {
                // CINFO is the base-2 logarithm of the LZ77 window size, minus eight.
                int cinfo = (cmf & 0xF0) >> 4;

                if (cinfo > 7)
                {
                    if (isCriticalChunk)
                    {
                        // Values of CINFO above 7 are not allowed in RFC1950.
                        // CINFO is not defined in this specification for CM not equal to 8.
                        throw new ImageFormatException($"Invalid window size for ZLIB header: cinfo={cinfo}");
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
            {
                if (isCriticalChunk)
                {
                    throw new ImageFormatException($"Bad method for ZLIB header: cmf={cmf}");
                }
                else
                {
                    return false;
                }
            }

            // The preset dictionary.
            bool fdict = (flag & 32) != 0;
            if (fdict)
            {
                // We don't need this for inflate so simply skip by the next four bytes.
                // https://tools.ietf.org/html/rfc1950#page-6
                this.innerStream.Read(ChecksumBuffer, 0, 4);
                this.currentDataRemaining -= 4;
            }

            // Initialize the deflate BufferedReadStream.
            this.CompressedStream = new DeflateStream(this, CompressionMode.Decompress, true);

            return true;
        }
    }
}
