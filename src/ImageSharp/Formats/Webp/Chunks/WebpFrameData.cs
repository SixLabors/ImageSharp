// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Chunks;

internal readonly struct WebpFrameData
{
    /// <summary>
    /// X(3) + Y(3) + Width(3) + Height(3) + Duration(3) + 1 byte for flags.
    /// </summary>
    public const uint HeaderSize = 16;

    public WebpFrameData(uint dataSize, uint x, uint y, uint width, uint height, uint duration, FrameBlendMode blendingMethod, FrameDisposalMode disposalMethod)
    {
        this.DataSize = dataSize;
        this.X = x;
        this.Y = y;
        this.Width = width;
        this.Height = height;
        this.Duration = duration;
        this.DisposalMethod = disposalMethod;
        this.BlendingMethod = blendingMethod;
    }

    public WebpFrameData(uint dataSize, uint x, uint y, uint width, uint height, uint duration, int flags)
        : this(
            dataSize,
            x,
            y,
            width,
            height,
            duration,
            (flags & 2) == 0 ? FrameBlendMode.Over : FrameBlendMode.Source,
            (flags & 1) == 1 ? FrameDisposalMode.RestoreToBackground : FrameDisposalMode.DoNotDispose)
    {
    }

    public WebpFrameData(uint x, uint y, uint width, uint height, uint duration, FrameBlendMode blendingMethod, FrameDisposalMode disposalMethod)
        : this(0, x, y, width, height, duration, blendingMethod, disposalMethod)
    {
    }

    /// <summary>
    /// Gets the animation chunk size.
    /// </summary>
    public uint DataSize { get; }

    /// <summary>
    /// Gets the X coordinate of the upper left corner of the frame is Frame X * 2.
    /// </summary>
    public uint X { get; }

    /// <summary>
    /// Gets the Y coordinate of the upper left corner of the frame is Frame Y * 2.
    /// </summary>
    public uint Y { get; }

    /// <summary>
    /// Gets the width of the frame.
    /// </summary>
    public uint Width { get; }

    /// <summary>
    /// Gets the height of the frame.
    /// </summary>
    public uint Height { get; }

    /// <summary>
    /// Gets the time to wait before displaying the next frame, in 1 millisecond units.
    /// Note the interpretation of frame duration of 0 (and often smaller then 10) is implementation defined.
    /// </summary>
    public uint Duration { get; }

    /// <summary>
    /// Gets how transparent pixels of the current frame are to be blended with corresponding pixels of the previous canvas.
    /// </summary>
    public FrameBlendMode BlendingMethod { get; }

    /// <summary>
    /// Gets how the current frame is to be treated after it has been displayed (before rendering the next frame) on the canvas.
    /// </summary>
    public FrameDisposalMode DisposalMethod { get; }

    public Rectangle Bounds => new((int)this.X, (int)this.Y, (int)this.Width, (int)this.Height);

    /// <summary>
    /// Writes the animation frame(<see cref="WebpChunkType.FrameData"/>) to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    public long WriteHeaderTo(Stream stream)
    {
        byte flags = 0;

        if (this.BlendingMethod is FrameBlendMode.Source)
        {
            // Set blending flag.
            flags |= 2;
        }

        if (this.DisposalMethod is FrameDisposalMode.RestoreToBackground)
        {
            // Set disposal flag.
            flags |= 1;
        }

        long pos = RiffHelper.BeginWriteChunk(stream, (uint)WebpChunkType.FrameData);

        WebpChunkParsingUtils.WriteUInt24LittleEndian(stream, (uint)Math.Round(this.X / 2f));
        WebpChunkParsingUtils.WriteUInt24LittleEndian(stream, (uint)Math.Round(this.Y / 2f));
        WebpChunkParsingUtils.WriteUInt24LittleEndian(stream, this.Width - 1);
        WebpChunkParsingUtils.WriteUInt24LittleEndian(stream, this.Height - 1);
        WebpChunkParsingUtils.WriteUInt24LittleEndian(stream, this.Duration);
        stream.WriteByte(flags);

        return pos;
    }

    /// <summary>
    /// Reads the animation frame header.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>Animation frame data.</returns>
    public static WebpFrameData Parse(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[4];

        return new WebpFrameData(
            dataSize: WebpChunkParsingUtils.ReadChunkSize(stream, buffer),
            x: WebpChunkParsingUtils.ReadUInt24LittleEndian(stream, buffer) * 2,
            y: WebpChunkParsingUtils.ReadUInt24LittleEndian(stream, buffer) * 2,
            width: WebpChunkParsingUtils.ReadUInt24LittleEndian(stream, buffer) + 1,
            height: WebpChunkParsingUtils.ReadUInt24LittleEndian(stream, buffer) + 1,
            duration: WebpChunkParsingUtils.ReadUInt24LittleEndian(stream, buffer),
            flags: stream.ReadByte());
    }
}
