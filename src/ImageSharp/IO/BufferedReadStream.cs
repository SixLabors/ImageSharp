// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.IO
{
    /// <summary>
    /// A readonly stream that add a secondary level buffer in addition to native stream
    /// buffered reading to reduce the overhead of small incremental reads.
    /// </summary>
    internal sealed unsafe class BufferedReadStream : Stream
    {
        /// <summary>
        /// The length, in bytes, of the underlying buffer.
        /// </summary>
        public const int BufferLength = 8192;

        private const int MaxBufferIndex = BufferLength - 1;

        private readonly Stream stream;

        private readonly int streamLength;

        private readonly byte[] readBuffer;

        private MemoryHandle readBufferHandle;

        private readonly byte* pinnedReadBuffer;

        // Index within our buffer, not reader position.
        private int readBufferIndex;

        // Matches what the stream position would be without buffering
        private int readerPosition;

        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="BufferedReadStream"/> class.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        public BufferedReadStream(Stream stream)
        {
            Guard.IsTrue(stream.CanRead, nameof(stream), "Stream must be readable.");
            Guard.IsTrue(stream.CanSeek, nameof(stream), "Stream must be seekable.");

            // Ensure all underlying buffers have been flushed before we attempt to read the stream.
            // User streams may have opted to throw from Flush if CanWrite is false
            // (although the abstract Stream does not do so).
            if (stream.CanWrite)
            {
                stream.Flush();
            }

            this.stream = stream;
            this.Position = (int)stream.Position;
            this.streamLength = (int)stream.Length;

            this.readBuffer = ArrayPool<byte>.Shared.Rent(BufferLength);
            this.readBufferHandle = new Memory<byte>(this.readBuffer).Pin();
            this.pinnedReadBuffer = (byte*)this.readBufferHandle.Pointer;

            // This triggers a full read on first attempt.
            this.readBufferIndex = BufferLength;
        }

        /// <summary>
        /// Gets the length, in bytes, of the stream.
        /// </summary>
        public override long Length => this.streamLength;

        /// <summary>
        /// Gets or sets the current position within the stream.
        /// </summary>
        public override long Position
        {
            get => this.readerPosition;

            set
            {
                // Only reset readBufferIndex if we are out of bounds of our working buffer
                // otherwise we should simply move the value by the diff.
                int v = (int)value;
                if (this.IsInReadBuffer(v, out int index))
                {
                    this.readBufferIndex = index;
                    this.readerPosition = v;
                }
                else
                {
                    this.readerPosition = v;
                    this.stream.Seek(value, SeekOrigin.Begin);
                    this.readBufferIndex = BufferLength;
                }
            }
        }

        /// <inheritdoc/>
        public override bool CanRead { get; } = true;

        /// <inheritdoc/>
        public override bool CanSeek { get; } = true;

        /// <inheritdoc/>
        public override bool CanWrite { get; } = false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int ReadByte()
        {
            if (this.readerPosition >= this.streamLength)
            {
                return -1;
            }

            // Our buffer has been read.
            // We need to refill and start again.
            if (this.readBufferIndex > MaxBufferIndex)
            {
                this.FillReadBuffer();
            }

            this.readerPosition++;
            return this.pinnedReadBuffer[this.readBufferIndex++];
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Read(byte[] buffer, int offset, int count)
        {
            // Too big for our buffer. Read directly from the stream.
            if (count > BufferLength)
            {
                return this.ReadToBufferDirectSlow(buffer, offset, count);
            }

            // Too big for remaining buffer but less than entire buffer length
            // Copy to buffer then read from there.
            if (count + this.readBufferIndex > BufferLength)
            {
                return this.ReadToBufferViaCopySlow(buffer, offset, count);
            }

            return this.ReadToBufferViaCopyFast(buffer, offset, count);
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            // Reset the stream position to match reader position.
            if (this.readerPosition != this.stream.Position)
            {
                this.stream.Seek(this.readerPosition, SeekOrigin.Begin);
                this.readerPosition = (int)this.stream.Position;
            }

            // Reset to trigger full read on next attempt.
            this.readBufferIndex = BufferLength;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
            {
                this.Position = offset;
            }
            else
            {
                this.Position += offset;
            }

            return this.readerPosition;
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This operation is not supported in <see cref="BufferedReadStream"/>.</exception>
        public override void SetLength(long value)
            => throw new NotSupportedException();

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">This operation is not supported in <see cref="BufferedReadStream"/>.</exception>
        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                this.readBufferHandle.Dispose();
                ArrayPool<byte>.Shared.Return(this.readBuffer);
                this.Flush();

                base.Dispose(true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetPositionDifference(int p) => p - this.readerPosition;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsInReadBuffer(int p, out int index)
        {
            index = this.GetPositionDifference(p) + this.readBufferIndex;
            return index > -1 && index < BufferLength;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void FillReadBuffer()
        {
            if (this.readerPosition != this.stream.Position)
            {
                this.stream.Seek(this.readerPosition, SeekOrigin.Begin);
            }

            this.stream.Read(this.readBuffer, 0, BufferLength);
            this.readBufferIndex = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadToBufferViaCopyFast(byte[] buffer, int offset, int count)
        {
            int n = this.GetCopyCount(count);
            this.CopyBytes(buffer, offset, n);

            this.readerPosition += n;
            this.readBufferIndex += n;

            return n;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int ReadToBufferViaCopySlow(byte[] buffer, int offset, int count)
        {
            // Refill our buffer then copy.
            this.FillReadBuffer();

            return this.ReadToBufferViaCopyFast(buffer, offset, count);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int ReadToBufferDirectSlow(byte[] buffer, int offset, int count)
        {
            // Read to target but don't copy to our read buffer.
            if (this.readerPosition != this.stream.Position)
            {
                this.stream.Seek(this.readerPosition, SeekOrigin.Begin);
            }

            int n = this.stream.Read(buffer, offset, count);
            this.Position += n;

            return n;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCopyCount(int count)
        {
            int n = this.streamLength - this.readerPosition;
            if (n > count)
            {
                n = count;
            }

            if (n < 0)
            {
                n = 0;
            }

            return n;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyBytes(byte[] buffer, int offset, int count)
        {
            // Same as MemoryStream.
            if (count < 9)
            {
                int byteCount = count;
                int read = this.readBufferIndex;
                byte* pinned = this.pinnedReadBuffer;

                while (--byteCount > -1)
                {
                    buffer[offset + byteCount] = pinned[read + byteCount];
                }
            }
            else
            {
                Buffer.BlockCopy(this.readBuffer, this.readBufferIndex, buffer, offset, count);
            }
        }
    }
}
