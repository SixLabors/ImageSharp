// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Utility class to encapsulate a sub-portion of another <see cref="Stream"/>.
    /// </summary>
    /// <remarks>
    /// Note that disposing of the <see cref="SubStream"/> does not dispose the underlying
    /// <see cref="Stream"/>.
    /// </remarks>
    internal class SubStream : Stream
    {
        private Stream innerStream;
        private long offset;
        private long endOffset;
        private long length;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubStream"/> class.
        /// </summary>
        /// <param name="innerStream">The underlying <see cref="Stream"/> to wrap.</param>
        /// <param name="length">The length of the sub-stream.</param>
        /// <remarks>
        /// Note that calling the sub-stream with start from the current offset of the
        /// underlying <see cref="Stream"/>
        /// </remarks>
        public SubStream(Stream innerStream, long length)
        {
            this.innerStream = innerStream;
            this.offset = this.innerStream.Position;
            this.endOffset = this.offset + length;
            this.length = length;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SubStream"/> class.
        /// </summary>
        /// <param name="innerStream">The underlying <see cref="Stream"/> to wrap.</param>
        /// <param name="offset">The offset of the sub-stream within the underlying <see cref="Stream"/>.</param>
        /// <param name="length">The length of the sub-stream.</param>
        /// <remarks>
        /// Note that calling the constructor will immediately move the underlying
        /// <see cref="Stream"/> to the specified offset.
        /// </remarks>
        public SubStream(Stream innerStream, long offset, long length)
        {
            this.innerStream = innerStream;
            this.offset = offset;
            this.endOffset = offset + length;
            this.length = length;

            innerStream.Seek(offset, SeekOrigin.Begin);
        }

        /// <inheritdoc/>
        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        /// <inheritdoc/>
        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public override bool CanSeek
        {
            get
            {
                return this.innerStream.CanSeek;
            }
        }

        /// <inheritdoc/>
        public override long Length
        {
            get
            {
                return this.length;
            }
        }

        /// <inheritdoc/>
        public override long Position
        {
            get
            {
                return this.innerStream.Position - this.offset;
            }

            set
            {
                this.Seek(value, SeekOrigin.Begin);
            }
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            long bytesRemaining = this.endOffset - this.innerStream.Position;

            if (bytesRemaining < count)
            {
                count = (int)bytesRemaining;
            }

            return this.innerStream.Read(buffer, offset, count);
        }

        /// <inheritdoc/>
        public override int ReadByte()
        {
            if (this.innerStream.Position < this.endOffset)
            {
                return this.innerStream.ReadByte();
            }
            else
            {
                return -1;
            }
        }

        /// <inheritdoc/>
        public override void Write(byte[] array, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override void WriteByte(byte value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Current:
                    return this.innerStream.Seek(offset, SeekOrigin.Current) - this.offset;
                case SeekOrigin.Begin:
                    return this.innerStream.Seek(this.offset + offset, SeekOrigin.Begin) - this.offset;
                case SeekOrigin.End:
                    return this.innerStream.Seek(this.endOffset - offset, SeekOrigin.Begin) - this.offset;
                default:
                    throw new ArgumentException("Invalid seek origin.");
            }
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }
}