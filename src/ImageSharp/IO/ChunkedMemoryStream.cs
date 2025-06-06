// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.IO;

/// <summary>
/// Provides an in-memory stream composed of non-contiguous chunks that doesn't need to be resized.
/// Chunks are allocated by the <see cref="MemoryAllocator"/> assigned via the constructor
/// and is designed to take advantage of buffer pooling when available.
/// </summary>
internal sealed class ChunkedMemoryStream : Stream
{
    private readonly MemoryChunkBuffer memoryChunkBuffer;
    private long length;
    private long position;
    private int bufferIndex;
    private int chunkIndex;
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkedMemoryStream"/> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    public ChunkedMemoryStream(MemoryAllocator allocator)
        => this.memoryChunkBuffer = new(allocator);

    /// <inheritdoc/>
    public override bool CanRead => !this.isDisposed;

    /// <inheritdoc/>
    public override bool CanSeek => !this.isDisposed;

    /// <inheritdoc/>
    public override bool CanWrite => !this.isDisposed;

    /// <inheritdoc/>
    public override long Length
    {
        get
        {
            this.EnsureNotDisposed();
            return this.length;
        }
    }

    /// <inheritdoc/>
    public override long Position
    {
        get
        {
            this.EnsureNotDisposed();
            return this.position;
        }

        set
        {
            this.EnsureNotDisposed();
            this.SetPosition(value);
        }
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        this.EnsureNotDisposed();

        this.Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => this.Position + offset,
            SeekOrigin.End => this.Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(offset)),
        };

        return this.position;
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int ReadByte()
    {
        Unsafe.SkipInit(out byte b);
        return this.Read(MemoryMarshal.CreateSpan(ref b, 1)) == 1 ? b : -1;
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        Guard.NotNull(buffer, nameof(buffer));
        Guard.MustBeGreaterThanOrEqualTo(offset, 0, nameof(offset));
        Guard.MustBeGreaterThanOrEqualTo(count, 0, nameof(count));

        const string bufferMessage = "Offset subtracted from the buffer length is less than count.";
        Guard.IsFalse(buffer.Length - offset < count, nameof(buffer), bufferMessage);

        return this.Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        this.EnsureNotDisposed();

        int offset = 0;
        int count = buffer.Length;

        long remaining = this.length - this.position;
        if (remaining <= 0)
        {
            // Already at the end of the stream, nothing to read
            return 0;
        }

        if (remaining > count)
        {
            remaining = count;
        }

        // 'remaining' can be less than the provided buffer length.
        int bytesToRead = (int)remaining;
        int bytesRead = 0;
        while (bytesToRead > 0 && this.bufferIndex != this.memoryChunkBuffer.Length)
        {
            bool moveToNextChunk = false;
            MemoryChunk chunk = this.memoryChunkBuffer[this.bufferIndex];
            int n = bytesToRead;
            int remainingBytesInCurrentChunk = chunk.Length - this.chunkIndex;
            if (n >= remainingBytesInCurrentChunk)
            {
                n = remainingBytesInCurrentChunk;
                moveToNextChunk = true;
            }

            // Read n bytes from the current chunk
            chunk.Buffer.Memory.Span.Slice(this.chunkIndex, n).CopyTo(buffer.Slice(offset, n));
            bytesToRead -= n;
            offset += n;
            bytesRead += n;

            if (moveToNextChunk)
            {
                this.chunkIndex = 0;
                this.bufferIndex++;
            }
            else
            {
                this.chunkIndex += n;
            }
        }

        this.position += bytesRead;
        return bytesRead;
    }

    /// <inheritdoc/>
    public override void WriteByte(byte value)
        => this.Write(MemoryMarshal.CreateSpan(ref value, 1));

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        Guard.NotNull(buffer, nameof(buffer));
        Guard.MustBeGreaterThanOrEqualTo(offset, 0, nameof(offset));
        Guard.MustBeGreaterThanOrEqualTo(count, 0, nameof(count));

        const string bufferMessage = "Offset subtracted from the buffer length is less than count.";
        Guard.IsFalse(buffer.Length - offset < count, nameof(buffer), bufferMessage);

        this.Write(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        this.EnsureNotDisposed();

        int offset = 0;
        int count = buffer.Length;

        long remaining = this.memoryChunkBuffer.Length - this.position;

        // Ensure we have enough capacity to write the data.
        while (remaining < count)
        {
            this.memoryChunkBuffer.Expand();
            remaining = this.memoryChunkBuffer.Length - this.position;
        }

        int bytesToWrite = count;
        int bytesWritten = 0;
        while (bytesToWrite > 0 && this.bufferIndex != this.memoryChunkBuffer.Length)
        {
            bool moveToNextChunk = false;
            MemoryChunk chunk = this.memoryChunkBuffer[this.bufferIndex];
            int n = bytesToWrite;
            int remainingBytesInCurrentChunk = chunk.Length - this.chunkIndex;
            if (n >= remainingBytesInCurrentChunk)
            {
                n = remainingBytesInCurrentChunk;
                moveToNextChunk = true;
            }

            // Write n bytes to the current chunk
            buffer.Slice(offset, n).CopyTo(chunk.Buffer.Slice(this.chunkIndex, n));
            bytesToWrite -= n;
            offset += n;
            bytesWritten += n;

            if (moveToNextChunk)
            {
                this.chunkIndex = 0;
                this.bufferIndex++;
            }
            else
            {
                this.chunkIndex += n;
            }
        }

        this.position += bytesWritten;
        this.length += bytesWritten;
    }

    /// <summary>
    /// Writes the entire contents of this memory stream to another stream.
    /// </summary>
    /// <param name="stream">The stream to write this memory stream to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The current or target stream is closed.</exception>
    public void WriteTo(Stream stream)
    {
        Guard.NotNull(stream, nameof(stream));
        this.EnsureNotDisposed();

        this.Position = 0;

        long remaining = this.length - this.position;
        if (remaining <= 0)
        {
            // Already at the end of the stream, nothing to read
            return;
        }

        int bytesToRead = (int)remaining;
        int bytesRead = 0;
        while (bytesToRead > 0 && this.bufferIndex != this.memoryChunkBuffer.Length)
        {
            bool moveToNextChunk = false;
            MemoryChunk chunk = this.memoryChunkBuffer[this.bufferIndex];
            int n = bytesToRead;
            int remainingBytesInCurrentChunk = chunk.Length - this.chunkIndex;
            if (n >= remainingBytesInCurrentChunk)
            {
                n = remainingBytesInCurrentChunk;
                moveToNextChunk = true;
            }

            // Read n bytes from the current chunk
            stream.Write(chunk.Buffer.Memory.Span.Slice(this.chunkIndex, n));
            bytesToRead -= n;
            bytesRead += n;

            if (moveToNextChunk)
            {
                this.chunkIndex = 0;
                this.bufferIndex++;
            }
            else
            {
                this.chunkIndex += n;
            }
        }

        this.position += bytesRead;
    }

    /// <summary>
    /// Writes the stream contents to a byte array, regardless of the <see cref="Position"/> property.
    /// </summary>
    /// <returns>A new <see cref="T:byte[]"/>.</returns>
    public byte[] ToArray()
    {
        this.EnsureNotDisposed();
        long position = this.position;
        byte[] copy = new byte[this.length];

        this.Position = 0;
        _ = this.Read(copy, 0, copy.Length);
        this.Position = position;
        return copy;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (this.isDisposed)
        {
            return;
        }

        try
        {
            this.isDisposed = true;
            if (disposing)
            {
                this.memoryChunkBuffer.Dispose();
            }

            this.bufferIndex = 0;
            this.chunkIndex = 0;
            this.position = 0;
            this.length = 0;
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private void SetPosition(long value)
    {
        long newPosition = value;
        if (newPosition < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        this.position = newPosition;

        // Find the current chunk & current chunk index
        int currentChunkIndex = 0;
        long offset = newPosition;

        // If the new position is greater than the length of the stream, set the position to the end of the stream
        if (offset > 0 && offset >= this.memoryChunkBuffer.Length)
        {
            this.bufferIndex = this.memoryChunkBuffer.ChunkCount - 1;
            this.chunkIndex = this.memoryChunkBuffer[this.bufferIndex].Length - 1;
            return;
        }

        // Loop through the current chunks, as we increment the chunk index, we subtract the length of the chunk
        // from the offset. Once the offset is less than the length of the chunk, we have found the correct chunk.
        while (offset != 0)
        {
            int chunkLength = this.memoryChunkBuffer[currentChunkIndex].Length;
            if (offset < chunkLength)
            {
                // Found the correct chunk and the corresponding index
                break;
            }

            offset -= chunkLength;
            currentChunkIndex++;
        }

        this.bufferIndex = currentChunkIndex;

        // Safe to cast here as we know the offset is less than the chunk length.
        this.chunkIndex = (int)offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureNotDisposed()
    {
        if (this.isDisposed)
        {
            ThrowDisposed();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowDisposed() => throw new ObjectDisposedException(nameof(ChunkedMemoryStream), "The stream is closed.");

    private sealed class MemoryChunkBuffer : IDisposable
    {
        private readonly List<MemoryChunk> memoryChunks = new();
        private readonly MemoryAllocator allocator;
        private readonly int allocatorCapacity;
        private bool isDisposed;

        public MemoryChunkBuffer(MemoryAllocator allocator)
        {
            this.allocatorCapacity = allocator.GetBufferCapacityInBytes();
            this.allocator = allocator;
        }

        public int ChunkCount => this.memoryChunks.Count;

        public long Length { get; private set; }

        public MemoryChunk this[int index] => this.memoryChunks[index];

        public void Expand()
        {
            IMemoryOwner<byte> buffer =
                this.allocator.Allocate<byte>(Math.Min(this.allocatorCapacity, GetChunkSize(this.ChunkCount)));

            MemoryChunk chunk = new(buffer)
            {
                Length = buffer.Length()
            };

            this.memoryChunks.Add(chunk);
            this.Length += chunk.Length;
        }

        public void Dispose()
        {
            if (!this.isDisposed)
            {
                foreach (MemoryChunk chunk in this.memoryChunks)
                {
                    chunk.Dispose();
                }

                this.memoryChunks.Clear();
                this.Length = 0;
                this.isDisposed = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetChunkSize(int i)
        {
            // Increment chunks sizes with moderate speed, but without using too many buffers from the
            // same ArrayPool bucket of the default MemoryAllocator.
            // https://github.com/SixLabors/ImageSharp/pull/2006#issuecomment-1066244720
            const int b128K = 1 << 17;
            const int b4M = 1 << 22;
            return i < 16 ? b128K * (1 << (int)((uint)i / 4)) : b4M;
        }
    }

    private sealed class MemoryChunk : IDisposable
    {
        private bool isDisposed;

        public MemoryChunk(IMemoryOwner<byte> buffer) => this.Buffer = buffer;

        public IMemoryOwner<byte> Buffer { get; }

        public int Length { get; init; }

        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.Buffer.Dispose();
                this.isDisposed = true;
            }
        }
    }
}
