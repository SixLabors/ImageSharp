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
    internal sealed unsafe class BufferedReadStream2 : IDisposable
    {
        /// <summary>
        /// The length, in bytes, of the underlying buffer.
        /// </summary>
        public const int BufferLength = 8192;

        private const int MaxBufferIndex = BufferLength - 1;

        private readonly Stream stream;

        private readonly byte[] readBuffer;

        private MemoryHandle readBufferHandle;

        private readonly byte* pinnedReadBuffer;

        private int readBufferIndex;

        private readonly int length;

        private int position;

        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="BufferedReadStream2"/> class.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        public BufferedReadStream2(Stream stream)
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
            this.length = (int)stream.Length;

            this.readBuffer = ArrayPool<byte>.Shared.Rent(BufferLength);
            this.readBufferHandle = new Memory<byte>(this.readBuffer).Pin();
            this.pinnedReadBuffer = (byte*)this.readBufferHandle.Pointer;

            // This triggers a full read on first attempt.
            this.readBufferIndex = BufferLength;
        }

        /// <summary>
        /// Gets the length, in bytes, of the stream.
        /// </summary>
        public long Length => this.length;

        /// <summary>
        /// Gets or sets the current position within the stream.
        /// </summary>
        public long Position
        {
            get => this.position;

            set
            {
                // Only reset readIndex if we are out of bounds of our working buffer
                // otherwise we should simply move the value by the diff.
                int v = (int)value;
                if (this.IsInReadBuffer(v, out int index))
                {
                    this.readBufferIndex = index;
                    this.position = v;
                }
                else
                {
                    this.position = v;
                    this.stream.Seek(value, SeekOrigin.Begin);
                    this.readBufferIndex = BufferLength;
                }
            }
        }

        public bool CanRead { get; } = true;

        public bool CanSeek { get; } = true;

        public bool CanWrite { get; } = false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadByte()
        {
            if (this.position >= this.length)
            {
                return -1;
            }

            if (this.readBufferIndex > MaxBufferIndex)
            {
                this.FillReadBuffer();
            }

            this.position++;
            return this.pinnedReadBuffer[this.readBufferIndex++];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read(byte[] buffer, int offset, int count)
        {
            if (count > BufferLength)
            {
                return this.ReadToBufferDirectSlow(buffer, offset, count);
            }

            if (count + this.readBufferIndex > BufferLength)
            {
                return this.ReadToBufferViaCopySlow(buffer, offset, count);
            }

            // return this.ReadToBufferViaCopyFast(buffer, offset, count);
            int n = this.GetCopyCount(count);
            this.CopyBytes(buffer, offset, n);

            this.position += n;
            this.readBufferIndex += n;

            return n;
        }

        public void Flush()
        {
            // Reset the stream position.
            if (this.position != this.stream.Position)
            {
                this.stream.Seek(this.position, SeekOrigin.Begin);
                this.position = (int)this.stream.Position;
            }

            this.readBufferIndex = BufferLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Begin)
            {
                this.Position = offset;
            }
            else
            {
                this.Position += offset;
            }

            return this.position;
        }

        public void SetLength(long value)
            => throw new NotSupportedException();

        public void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                this.readBufferHandle.Dispose();
                ArrayPool<byte>.Shared.Return(this.readBuffer);
                this.Flush();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetPositionDifference(int p) => p - this.position;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsInReadBuffer(int p, out int index)
        {
            index = this.GetPositionDifference(p) + this.readBufferIndex;
            return index > -1 && index < BufferLength;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void FillReadBuffer()
        {
            if (this.position != this.stream.Position)
            {
                this.stream.Seek(this.position, SeekOrigin.Begin);
            }

            this.stream.Read(this.readBuffer, 0, BufferLength);
            this.readBufferIndex = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadToBufferViaCopyFast(byte[] buffer, int offset, int count)
        {
            int n = this.GetCopyCount(count);
            this.CopyBytes(buffer, offset, n);

            this.position += n;
            this.readBufferIndex += n;

            return n;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int ReadToBufferViaCopySlow(byte[] buffer, int offset, int count)
        {
            // Refill our buffer then copy.
            this.FillReadBuffer();

            // return this.ReadToBufferViaCopyFast(buffer, offset, count);
            int n = this.GetCopyCount(count);
            this.CopyBytes(buffer, offset, n);

            this.position += n;
            this.readBufferIndex += n;

            return n;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int ReadToBufferDirectSlow(byte[] buffer, int offset, int count)
        {
            // Read to target but don't copy to our read buffer.
            if (this.position != this.stream.Position)
            {
                this.stream.Seek(this.position, SeekOrigin.Begin);
            }

            int n = this.stream.Read(buffer, offset, count);
            this.Position += n;

            return n;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCopyCount(int count)
        {
            int n = this.length - this.position;
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
