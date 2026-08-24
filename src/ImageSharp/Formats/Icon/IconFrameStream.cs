// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Icon;

/// <summary>
/// Exposes one ICO or CUR directory entry as an isolated seekable stream.
/// </summary>
/// <remarks>
/// Embedded decoders accept seek offsets from their own headers. Bounding those seeks to <c>BytesInRes</c>
/// prevents a malformed BMP or PNG payload from consuming an adjacent icon resource.
/// </remarks>
internal sealed class IconFrameStream : Stream
{
    private readonly Stream stream;

    // start is absolute in the containing stream; position is always relative to this bounded resource.
    private long start;
    private long length;
    private long position;

    /// <summary>
    /// Initializes a new instance of the <see cref="IconFrameStream"/> class.
    /// </summary>
    /// <param name="stream">The containing icon stream.</param>
    public IconFrameStream(Stream stream)
        => this.stream = stream;

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => true;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => this.length;

    /// <inheritdoc/>
    public override long Position
    {
        get => this.position;
        set => this.Seek(value, SeekOrigin.Begin);
    }

    /// <summary>
    /// Repositions this stream over another image payload in the same containing stream.
    /// </summary>
    /// <param name="start">The absolute start of the image payload.</param>
    /// <param name="length">The image payload length.</param>
    public void Reset(long start, long length)
    {
        this.start = start;
        this.length = length;
        this.position = 0;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
        => this.Read(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        // Clamp every read to the entry boundary so a child decoder cannot consume the next resource.
        int count = (int)Math.Min(buffer.Length, this.length - this.position);
        if (count is 0)
        {
            return 0;
        }

        // The containing stream is shared by all entries, so synchronize its absolute position immediately before reading.
        this.stream.Position = this.start + this.position;
        int read = this.stream.Read(buffer[..count]);
        this.position += read;

        return read;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => this.position + offset,
            SeekOrigin.End => this.length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        // Casting rejects both negative offsets and offsets beyond Length with one bounds check.
        if ((ulong)target > (ulong)this.length)
        {
            throw new InvalidImageContentException("The embedded icon resource contains an invalid seek offset.");
        }

        // Delay moving the containing stream until Read; this keeps logical seeks isolated from sibling resources.
        this.position = target;
        return target;
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
