// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Compression.Zlib;

/// <summary>
/// A special stream deflating or compressing the bytes that are
/// written to it.  It uses a Deflater to perform actual deflating.
/// </summary>
internal sealed class DeflaterOutputStream : Stream
{
    private const int BufferLength = 512;
    private IMemoryOwner<byte> memoryOwner;
    private readonly Memory<byte> buffer;
    private Deflater deflater;
    private readonly Stream rawStream;
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeflaterOutputStream"/> class.
    /// </summary>
    /// <param name="memoryAllocator">The memory allocator to use for buffer allocations.</param>
    /// <param name="rawStream">The output stream where deflated output is written.</param>
    /// <param name="compressionLevel">The compression level.</param>
    public DeflaterOutputStream(MemoryAllocator memoryAllocator, Stream rawStream, int compressionLevel)
    {
        this.rawStream = rawStream;
        this.memoryOwner = memoryAllocator.Allocate<byte>(BufferLength);
        this.buffer = this.memoryOwner.Memory;
        this.deflater = new Deflater(memoryAllocator, compressionLevel);
    }

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => this.rawStream.CanWrite;

    /// <inheritdoc/>
    public override long Length => this.rawStream.Length;

    /// <inheritdoc/>
    public override long Position
    {
        get => this.rawStream.Position;

        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int ReadByte() => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Flush()
    {
        this.deflater.Flush();
        this.Deflate(true);
        this.rawStream.Flush();
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        this.deflater.SetInput(buffer, offset, count);
        this.Deflate();
    }

    private void Deflate() => this.Deflate(false);

    private void Deflate(bool flushing)
    {
        while (flushing || !this.deflater.IsNeedingInput)
        {
            int deflateCount = this.deflater.Deflate(this.buffer.Span, 0, BufferLength);

            if (deflateCount <= 0)
            {
                break;
            }

            this.rawStream.Write(this.buffer.Span[..deflateCount]);
        }

        if (!this.deflater.IsNeedingInput)
        {
            DeflateThrowHelper.ThrowNoDeflate();
        }
    }

    private void Finish()
    {
        this.deflater.Finish();
        while (!this.deflater.IsFinished)
        {
            int len = this.deflater.Deflate(this.buffer.Span, 0, BufferLength);
            if (len <= 0)
            {
                break;
            }

            this.rawStream.Write(this.buffer.Span[..len]);
        }

        if (!this.deflater.IsFinished)
        {
            DeflateThrowHelper.ThrowNoDeflate();
        }

        this.rawStream.Flush();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.isDisposed)
        {
            if (disposing)
            {
                this.Finish();
                this.deflater.Dispose();
                this.memoryOwner.Dispose();
            }

            this.isDisposed = true;
            base.Dispose(disposing);
        }
    }
}
