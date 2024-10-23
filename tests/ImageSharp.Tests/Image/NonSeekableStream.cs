// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests;

internal class NonSeekableStream : Stream
{
    private readonly Stream dataStream;

    public NonSeekableStream(Stream dataStream)
        => this.dataStream = dataStream;

    public override bool CanRead => this.dataStream.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => this.dataStream.CanWrite;

    public override bool CanTimeout => this.dataStream.CanTimeout;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int ReadTimeout
    {
        get => this.dataStream.ReadTimeout;
        set => this.dataStream.ReadTimeout = value;
    }

    public override int WriteTimeout
    {
        get => this.dataStream.WriteTimeout;
        set => this.dataStream.WriteTimeout = value;
    }

    public override void Flush() => this.dataStream.Flush();

    public override int ReadByte() => this.dataStream.ReadByte();

    public override int Read(byte[] buffer, int offset, int count)
        => this.dataStream.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer)
        => this.dataStream.Read(buffer);

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        => this.dataStream.BeginRead(buffer, offset, count, callback, state);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => this.dataStream.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => this.dataStream.ReadAsync(buffer, cancellationToken);

    public override int EndRead(IAsyncResult asyncResult)
    => this.dataStream.EndRead(asyncResult);

    public override void WriteByte(byte value) => this.dataStream.WriteByte(value);

    public override void Write(ReadOnlySpan<byte> buffer) => this.dataStream.Write(buffer);

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        => this.dataStream.BeginWrite(buffer, offset, count, callback, state);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => this.dataStream.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => this.dataStream.WriteAsync(buffer, cancellationToken);

    public override void EndWrite(IAsyncResult asyncResult) => this.dataStream.EndWrite(asyncResult);

    public override void CopyTo(Stream destination, int bufferSize) => this.dataStream.CopyTo(destination, bufferSize);

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        => this.dataStream.CopyToAsync(destination, bufferSize, cancellationToken);

    public override Task FlushAsync(CancellationToken cancellationToken) => this.dataStream.FlushAsync(cancellationToken);

    public override void Close() => this.dataStream.Close();

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
        => this.dataStream.Write(buffer, offset, count);
}
