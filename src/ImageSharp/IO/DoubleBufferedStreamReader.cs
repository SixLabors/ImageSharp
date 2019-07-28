// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

using SixLabors.Memory;

namespace SixLabors.ImageSharp.IO
{
    /// <summary>
    /// A stream reader that add a secondary level buffer in addition to native stream buffered reading
    /// to reduce the overhead of small incremental reads.
    /// </summary>
    internal sealed unsafe class DoubleBufferedStreamReader : IDisposable
    {
        /// <summary>
        /// The length, in bytes, of the buffering chunk.
        /// </summary>
        public const int ChunkLength = 8192;

        private const int MaxChunkIndex = ChunkLength - 1;

        private readonly Stream stream;

        private readonly IManagedByteBuffer managedBuffer;

        private MemoryHandle handle;

        private readonly byte* pinnedChunk;

        private readonly byte[] bufferChunk;

        private readonly int length;

        private int chunkIndex;

        private int position;

        /// <summary>
        /// Initializes a new instance of the <see cref="DoubleBufferedStreamReader"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The <see cref="MemoryAllocator"/> to use for buffer allocations.</param>
        /// <param name="stream">The input stream.</param>
        public DoubleBufferedStreamReader(MemoryAllocator memoryAllocator, Stream stream)
        {
            this.stream = stream;
            this.Position = (int)stream.Position;
            this.length = (int)stream.Length;
            this.managedBuffer = memoryAllocator.AllocateManagedByteBuffer(ChunkLength);
            this.bufferChunk = this.managedBuffer.Array;
            this.handle = this.managedBuffer.Memory.Pin();
            this.pinnedChunk = (byte*)this.handle.Pointer;
            this.chunkIndex = ChunkLength;
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
                // Only reset chunkIndex if we are out of bounds of our working chunk
                // otherwise we should simply move the value by the diff.
                int v = (int)value;
                if (this.IsInChunk(v, out int index))
                {
                    this.chunkIndex = index;
                    this.position = v;
                }
                else
                {
                    this.position = v;
                    this.stream.Seek(value, SeekOrigin.Begin);
                    this.chunkIndex = ChunkLength;
                }
            }
        }

        /// <summary>
        /// Reads a byte from the stream and advances the position within the stream by one
        /// byte, or returns -1 if at the end of the stream.
        /// </summary>
        /// <returns>The unsigned byte cast to an <see cref="int"/>, or -1 if at the end of the stream.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public int ReadByte()
        {
            if (this.position >= this.length)
            {
                return -1;
            }

            if (this.chunkIndex > MaxChunkIndex)
            {
                this.FillChunk();
            }

            this.position++;
            return this.pinnedChunk[this.chunkIndex++];
        }

        /// <summary>
        /// Skips the number of bytes in the stream
        /// </summary>
        /// <param name="count">The number of bytes to skip.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Skip(int count) => this.Position += count;

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
        [MethodImpl(InliningOptions.ShortMethod)]
        public int Read(byte[] buffer, int offset, int count)
        {
            if (count > ChunkLength)
            {
                return this.ReadToBufferSlow(buffer, offset, count);
            }

            if (count + this.chunkIndex > ChunkLength)
            {
                return this.ReadToChunkSlow(buffer, offset, count);
            }

            int n = this.GetCopyCount(count);
            this.CopyBytes(buffer, offset, n);

            this.position += n;
            this.chunkIndex += n;
            return n;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.handle.Dispose();
            this.managedBuffer?.Dispose();
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private int GetPositionDifference(int p) => p - this.position;

        [MethodImpl(InliningOptions.ShortMethod)]
        private bool IsInChunk(int p, out int index)
        {
            index = this.GetPositionDifference(p) + this.chunkIndex;
            return index > -1 && index < ChunkLength;
        }

        [MethodImpl(InliningOptions.ColdPath)]
        private void FillChunk()
        {
            if (this.position != this.stream.Position)
            {
                this.stream.Seek(this.position, SeekOrigin.Begin);
            }

            this.stream.Read(this.bufferChunk, 0, ChunkLength);
            this.chunkIndex = 0;
        }

        [MethodImpl(InliningOptions.ColdPath)]
        private int ReadToChunkSlow(byte[] buffer, int offset, int count)
        {
            // Refill our buffer then copy.
            this.FillChunk();

            int n = this.GetCopyCount(count);
            this.CopyBytes(buffer, offset, n);

            this.position += n;
            this.chunkIndex += n;

            return n;
        }

        [MethodImpl(InliningOptions.ColdPath)]
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

        [MethodImpl(InliningOptions.ShortMethod)]
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

        [MethodImpl(InliningOptions.ShortMethod)]
        private void CopyBytes(byte[] buffer, int offset, int count)
        {
            if (count < 9)
            {
                int byteCount = count;
                int read = this.chunkIndex;
                byte* pinned = this.pinnedChunk;

                while (--byteCount > -1)
                {
                    buffer[offset + byteCount] = pinned[read + byteCount];
                }
            }
            else
            {
                Buffer.BlockCopy(this.bufferChunk, this.chunkIndex, buffer, offset, count);
            }
        }
    }
}