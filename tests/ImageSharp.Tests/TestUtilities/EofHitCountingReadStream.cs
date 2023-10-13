// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests.TestUtilities;

internal class EofHitCountingReadStream : Stream
{
    private readonly Stream innerStream;

    public EofHitCountingReadStream(byte[] data)
        : this(new MemoryStream(data))
    {
    }

    public EofHitCountingReadStream(Stream innerStream) => this.innerStream = innerStream;

    public int EofHitCount { get; private set; }

    public override bool CanRead => this.innerStream.CanRead;

    public override bool CanSeek => this.innerStream.CanSeek;

    public override bool CanWrite => this.innerStream.CanWrite;

    public override long Length => this.innerStream.Length;

    public override long Position
    {
        get => this.innerStream.Position;
        set => this.innerStream.Position = value;
    }

    public override void Flush() => this.innerStream.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => this.innerStream.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = this.innerStream.Read(buffer, offset, count);
        this.CheckEof(read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        int read = this.innerStream.Read(buffer);
        this.CheckEof(read);
        return read;
    }

    public override int ReadByte()
    {
        int val = this.innerStream.ReadByte();
        if (val < 0)
        {
            this.EofHitCount++;
        }

        return val;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int read = await this.innerStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        this.CheckEof(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = await this.innerStream.ReadAsync(buffer, cancellationToken);
        this.CheckEof(read);
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => this.innerStream.Seek(offset, origin);

    public override void SetLength(long value) => this.innerStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => this.innerStream.Write(buffer, offset, count);

    private void CheckEof(int read)
    {
        if (read == 0)
        {
            this.EofHitCount++;
        }
    }
}
