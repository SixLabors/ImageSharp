// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Memory;

// TODO: This could be useful elsewhere.
namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// A stream reader that add a secondary level buffer in addition to native stream buffered reading
    /// to reduce the overhead of small incremental reads.
    /// </summary>
    internal class DoubleBufferedStreamReader : IDisposable
    {
        /// <summary>
        /// The length, in bytes, of the chunk
        /// </summary>
        public const int ChunkLength = 4096;

        private readonly Stream stream;

        private readonly IManagedByteBuffer buffer;

        private readonly byte[] chunk;

        private int bytesRead;

        private long position;

        /// <summary>
        /// Initializes a new instance of the <see cref="DoubleBufferedStreamReader"/> class.
        /// </summary>
        /// <param name="memoryManager">The <see cref="MemoryManager"/> to use for buffer allocations.</param>
        /// <param name="stream">The input stream.</param>
        public DoubleBufferedStreamReader(MemoryManager memoryManager, Stream stream)
        {
            this.stream = stream;
            this.Length = stream.Length;

            this.buffer = memoryManager.AllocateCleanManagedByteBuffer(ChunkLength);
            this.chunk = this.buffer.Array;
        }

        /// <summary>
        /// Gets the length, in bytes, of the stream
        /// </summary>
        public long Length { get; }

        /// <summary>
        /// Gets or sets the current position within the stream
        /// </summary>
        public long Position
        {
            get
            {
                return this.position;
            }

            set
            {
                // Reset everything. It's easier than tracking.
                this.position = value;
                this.stream.Seek(this.position, SeekOrigin.Begin);
                this.bytesRead = ChunkLength;
            }
        }

        /// <summary>
        /// Reads a byte from the stream and advances the position within the stream by one
        /// byte, or returns -1 if at the end of the stream.
        /// </summary>
        /// <returns>The unsigned byte cast to an <see cref="int"/>, or -1 if at the end of the stream.</returns>
        public int ReadByte()
        {
            if (this.position >= this.Length)
            {
                return -1;
            }

            if (this.position == 0 || this.bytesRead >= ChunkLength)
            {
                this.stream.Seek(this.position, SeekOrigin.Begin);
                this.stream.Read(this.chunk, 0, ChunkLength);
                this.bytesRead = 0;
            }

            this.position++;
            return this.chunk[this.bytesRead++];
        }

        /// <summary>
        /// Skips the number of bytes in the stream
        /// </summary>
        /// <param name="count">The number of bytes to skip</param>
        public void Skip(int count)
        {
            this.position += count;
            this.bytesRead += count;
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
        public int Read(byte[] buffer, int offset, int count)
        {
            int n = 0;
            if (buffer.Length <= ChunkLength)
            {
                if (this.position == 0 || count + this.bytesRead > ChunkLength)
                {
                    // Refill our buffer then copy.
                    this.stream.Seek(this.position, SeekOrigin.Begin);
                    this.stream.Read(this.chunk, 0, ChunkLength);
                    this.bytesRead = 0;
                }

                Buffer.BlockCopy(this.chunk, this.bytesRead, buffer, offset, count);
                this.position += count;
                this.bytesRead += count;

                n = Math.Min(count, (int)(this.Length - this.position));
            }
            else
            {
                // Read to target but don't copy to our chunk.
                this.stream.Seek(this.position, SeekOrigin.Begin);
                n = this.stream.Read(buffer, offset, count);

                // Ensure next read fills the chunk
                this.bytesRead = ChunkLength;
                this.position += count;
            }

            return Math.Max(n, 0);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.buffer?.Dispose();
        }
    }
}