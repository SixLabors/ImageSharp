// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Runtime.CompilerServices;

using SixLabors.Memory;

namespace SixLabors.ImageSharp.IO
{
    /// <summary>
    /// A stream reader that add a secondary level buffer in addition to native stream buffered reading
    /// to reduce the overhead of small incremental reads.
    /// </summary>
    internal class DoubleBufferedStreamReader : IDisposable
    {
        /// <summary>
        /// The length, in bytes, of the buffering chunk
        /// </summary>
        public const int ChunkLength = 4096;

        private const int ChunkLengthMinusOne = ChunkLength - 1;

        private readonly Stream stream;

        private readonly IManagedByteBuffer managedBuffer;

        private readonly byte[] bufferChunk;

        private readonly int length;

        private int bytesRead;

        private int position;

        /// <summary>
        /// Initializes a new instance of the <see cref="DoubleBufferedStreamReader"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations.</param>
        /// <param name="stream">The input stream.</param>
        public DoubleBufferedStreamReader(MemoryAllocator memoryAllocator, Stream stream)
        {
            this.stream = stream;
            this.length = (int)stream.Length;
            this.managedBuffer = memoryAllocator.AllocateManagedByteBuffer(ChunkLength, AllocationOptions.Clean);
            this.bufferChunk = this.managedBuffer.Array;
        }

        /// <summary>
        /// Gets the length, in bytes, of the stream
        /// </summary>
        public long Length => this.length;

        /// <summary>
        /// Gets or sets the current position within the stream
        /// </summary>
        public long Position
        {
            get => this.position;

            set
            {
                // Reset everything. It's easier than tracking.
                this.position = (int)value;
                this.stream.Seek(this.position, SeekOrigin.Begin);
                this.bytesRead = ChunkLength;
            }
        }

        /// <summary>
        /// Reads a byte from the stream and advances the position within the stream by one
        /// byte, or returns -1 if at the end of the stream.
        /// </summary>
        /// <returns>The unsigned byte cast to an <see cref="int"/>, or -1 if at the end of the stream.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadByte()
        {
            if (this.position >= this.length)
            {
                return -1;
            }

            if (this.position == 0 || this.bytesRead > ChunkLengthMinusOne)
            {
                return this.ReadByteSlow();
            }

            this.position++;
            return this.bufferChunk[this.bytesRead++];
        }

        /// <summary>
        /// Skips the number of bytes in the stream
        /// </summary>
        /// <param name="count">The number of bytes to skip</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Skip(int count)
        {
            this.Position += count;
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream
        /// by the number of bytes read.
        /// </summary>
        /// <param name="buffer">
        /// An array of bytes. When this method returns, the buffer contains the specified
        /// byte array with the values between offset and (offset + count - 1) replaced by
        /// the bytes read from the current source.
        /// </param>
        /// <param name="offset">
        /// The zero-based byte offset in buffer at which to begin storing the data read
        /// from the current stream.
        /// </param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>
        /// The total number of bytes read into the buffer. This can be less than the number
        /// of bytes requested if that many bytes are not currently available, or zero (0)
        /// if the end of the stream has been reached.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read(byte[] buffer, int offset, int count)
        {
            if (buffer.Length > ChunkLength)
            {
                return this.ReadToBufferSlow(buffer, offset, count);
            }

            if (this.position == 0 || count + this.bytesRead > ChunkLength)
            {
                return this.ReadToChunkSlow(buffer, offset, count);
            }

            int n = this.GetCount(count);
            this.CopyBytes(buffer, offset, n);

            this.position += n;
            this.bytesRead += n;

            return n;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.managedBuffer?.Dispose();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int ReadByteSlow()
        {
            if (this.position != this.stream.Position)
            {
                this.stream.Seek(this.position, SeekOrigin.Begin);
            }

            this.stream.Read(this.bufferChunk, 0, ChunkLength);
            this.bytesRead = 0;

            this.position++;
            return this.bufferChunk[this.bytesRead++];
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int ReadToChunkSlow(byte[] buffer, int offset, int count)
        {
            // Refill our buffer then copy.
            if (this.position != this.stream.Position)
            {
                this.stream.Seek(this.position, SeekOrigin.Begin);
            }

            this.stream.Read(this.bufferChunk, 0, ChunkLength);
            this.bytesRead = 0;

            int n = this.GetCount(count);
            this.CopyBytes(buffer, offset, n);

            this.position += n;
            this.bytesRead += n;

            return n;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int ReadToBufferSlow(byte[] buffer, int offset, int count)
        {
            // Read to target but don't copy to our chunk.
            if (this.position != this.stream.Position)
            {
                this.stream.Seek(this.position, SeekOrigin.Begin);
            }

            int n = this.stream.Read(buffer, offset, count);
            this.Position += n;
            return n;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCount(int count)
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
            if (count < 9)
            {
                int byteCount = count;
                int read = this.bytesRead;
                byte[] chunk = this.bufferChunk;

                while (--byteCount > -1)
                {
                    buffer[offset + byteCount] = chunk[read + byteCount];
                }
            }
            else
            {
                Buffer.BlockCopy(this.bufferChunk, this.bytesRead, buffer, offset, count);
            }
        }
    }
}