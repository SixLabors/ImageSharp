// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Compression.Zlib;

/// <summary>
/// A write-only stream that groups written bytes into fixed-length segments. Bytes are
/// collected in a pooled segment buffer; when the buffer is full the supplied delegate is
/// invoked with the completed segment and the buffer is reused. The final partial segment,
/// if any, is emitted on disposal. The delegate owns the destination; this stream writes
/// nowhere itself and is the write-side counterpart of <see cref="ChunkedReadStream"/>.
/// </summary>
internal sealed class ChunkedWriteStream : Stream
{
    /// <summary>
    /// The segment length used when the caller does not require a specific framing size.
    /// </summary>
    public const int DefaultSegmentLength = 64 * 1024;

    private readonly IMemoryOwner<byte> segmentOwner;
    private readonly Memory<byte> segment;
    private readonly Action<ReadOnlySpan<byte>> writeSegment;
    private int segmentFilled;
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkedWriteStream"/> class using <see cref="DefaultSegmentLength"/>.
    /// </summary>
    /// <param name="allocator">The memory allocator used to rent the segment buffer.</param>
    /// <param name="writeSegment">Invoked with each completed segment, and with the final partial segment on disposal.</param>
    public ChunkedWriteStream(MemoryAllocator allocator, Action<ReadOnlySpan<byte>> writeSegment)
        : this(allocator, DefaultSegmentLength, writeSegment)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkedWriteStream"/> class.
    /// </summary>
    /// <param name="allocator">The memory allocator used to rent the segment buffer.</param>
    /// <param name="segmentLength">The length of each completed segment.</param>
    /// <param name="writeSegment">Invoked with each completed segment, and with the final partial segment on disposal.</param>
    public ChunkedWriteStream(MemoryAllocator allocator, int segmentLength, Action<ReadOnlySpan<byte>> writeSegment)
    {
        this.segmentOwner = allocator.Allocate<byte>(segmentLength);
        this.segment = this.segmentOwner.Memory;
        this.writeSegment = writeSegment;
    }

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    /// <summary>
    /// Does nothing. A segment is emitted only when it is full or on disposal, so the segment
    /// length stays fixed however often the producer flushes.
    /// </summary>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void WriteByte(byte value)
    {
        this.segment.Span[this.segmentFilled++] = value;
        this.EmitIfFull();
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => this.Write(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Span<byte> segment = this.segment.Span;
        while (!buffer.IsEmpty)
        {
            int count = Math.Min(segment.Length - this.segmentFilled, buffer.Length);
            buffer[..count].CopyTo(segment[this.segmentFilled..]);
            this.segmentFilled += count;
            buffer = buffer[count..];
            this.EmitIfFull();
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (this.isDisposed)
        {
            return;
        }

        this.isDisposed = true;
        if (disposing)
        {
            // The producer has finished, so the partial segment is the final one.
            if (this.segmentFilled > 0)
            {
                this.writeSegment(this.segment.Span[..this.segmentFilled]);
                this.segmentFilled = 0;
            }

            this.segmentOwner.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Emits the segment buffer when it is full and resets it for reuse.
    /// </summary>
    private void EmitIfFull()
    {
        if (this.segmentFilled == this.segment.Length)
        {
            this.writeSegment(this.segment.Span);
            this.segmentFilled = 0;
        }
    }
}
