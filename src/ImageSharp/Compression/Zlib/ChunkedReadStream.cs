// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Compression.Zlib;

/// <summary>
/// A read-only stream over a sequence of length-delimited segments. Bytes are
/// pulled from the inner stream up to the current segment's remaining length;
/// when the segment is exhausted the supplied delegate is invoked to advance
/// to the next segment and return its length. The inner stream is not owned
/// and is not disposed.
/// </summary>
internal sealed class ChunkedReadStream : Stream
{
    private static readonly Func<int> GetDataNoOp = () => 0;

    private readonly BufferedReadStream innerStream;
    private readonly Func<int> getData;
    private int currentDataRemaining;

    public ChunkedReadStream(BufferedReadStream innerStream)
        : this(innerStream, GetDataNoOp)
    {
    }

    public ChunkedReadStream(BufferedReadStream innerStream, Func<int> getData)
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
    /// Sets the number of bytes available to read from the current segment.
    /// Must be called before reading each segment.
    /// </summary>
    public void SetCurrentSegmentLength(int bytes) => this.currentDataRemaining = bytes;

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
        if (this.currentDataRemaining is 0)
        {
            // Current segment is exhausted; ask the caller for the next one.
            this.currentDataRemaining = this.getData();

            if (this.currentDataRemaining is 0)
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
        while (this.currentDataRemaining is 0 && totalBytesRead < count)
        {
            this.currentDataRemaining = this.getData();

            if (this.currentDataRemaining is 0)
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
            if (bytesRead == 0)
            {
                return totalBytesRead;
            }

            totalBytesRead += bytesRead;
        }

        return totalBytesRead;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
