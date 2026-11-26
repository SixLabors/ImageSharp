// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Png.Chunks;

internal readonly struct FrameControl
{
    public const int Size = 26;

    public FrameControl(uint width, uint height)
        : this(0, width, height, 0, 0, 0, 0, default, default)
    {
    }

    public FrameControl(
        uint sequenceNumber,
        uint width,
        uint height,
        uint xOffset,
        uint yOffset,
        ushort delayNumerator,
        ushort delayDenominator,
        FrameDisposalMode disposalMode,
        FrameBlendMode blendMode)
    {
        this.SequenceNumber = sequenceNumber;
        this.Width = width;
        this.Height = height;
        this.XOffset = xOffset;
        this.YOffset = yOffset;
        this.DelayNumerator = delayNumerator;
        this.DelayDenominator = delayDenominator;
        this.DisposalMode = disposalMode;
        this.BlendMode = blendMode;
    }

    /// <summary>
    /// Gets the sequence number of the animation chunk, starting from 0
    /// </summary>
    public uint SequenceNumber { get; }

    /// <summary>
    /// Gets the width of the following frame
    /// </summary>
    public uint Width { get; }

    /// <summary>
    /// Gets the height of the following frame
    /// </summary>
    public uint Height { get; }

    /// <summary>
    /// Gets the X position at which to render the following frame
    /// </summary>
    public uint XOffset { get; }

    /// <summary>
    /// Gets the Y position at which to render the following frame
    /// </summary>
    public uint YOffset { get; }

    /// <summary>
    /// Gets the X limit at which to render the following frame
    /// </summary>
    public uint XMax => this.XOffset + this.Width;

    /// <summary>
    /// Gets the Y limit at which to render the following frame
    /// </summary>
    public uint YMax => this.YOffset + this.Height;

    /// <summary>
    /// Gets the frame delay fraction numerator
    /// </summary>
    public ushort DelayNumerator { get; }

    /// <summary>
    /// Gets the frame delay fraction denominator
    /// </summary>
    public ushort DelayDenominator { get; }

    /// <summary>
    /// Gets the type of frame area disposal to be done after rendering this frame
    /// </summary>
    public FrameDisposalMode DisposalMode { get; }

    /// <summary>
    /// Gets the type of frame area rendering for this frame
    /// </summary>
    public FrameBlendMode BlendMode { get; }

    public Rectangle Bounds => new((int)this.XOffset, (int)this.YOffset, (int)this.Width, (int)this.Height);

    /// <summary>
    /// Gets a value that tells if the frame control is valid for the image header.
    /// </summary>
    /// <remarks>
    /// The region must have pixels and must be inside the canvas. The sums use 64-bit values, so a large offset cannot wrap and pass the check. The blend_op
    /// value is 0 or 1. <see cref="Parse"/> maps each dispose_op value to a valid disposal mode, so the method does not check it. See PNG Third Edition,
    /// section 11.3.6.2 "fcTL Frame Control Chunk": https://www.w3.org/TR/png-3/#fcTL-chunk.
    /// </remarks>
    /// <param name="header">The image header.</param>
    /// <returns><see langword="true"/> if the frame control is valid. Otherwise <see langword="false"/>.</returns>
    public bool IsValid(PngHeader header)
        => this.Width > 0
        && this.Height > 0
        && (ulong)this.XOffset + this.Width <= (uint)header.Width
        && (ulong)this.YOffset + this.Height <= (uint)header.Height
        && this.BlendMode is FrameBlendMode.Source or FrameBlendMode.Over;

    /// <summary>
    /// Writes the fcTL to the given buffer.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    public void WriteTo(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt32BigEndian(buffer[..4], this.SequenceNumber);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[4..8], this.Width);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[8..12], this.Height);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[12..16], this.XOffset);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[16..20], this.YOffset);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[20..22], this.DelayNumerator);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[22..24], this.DelayDenominator);

        // APNG has no unspecified disposal. The method writes dispose_op 0, APNG_DISPOSE_OP_NONE, the same value as for DoNotDispose. See PNG Third Edition,
        // section 11.3.6.2 "fcTL Frame Control Chunk". Older ImageSharp versions wrote 255, which is not valid. Parse still reads that value from their files.
        buffer[24] = (byte)(this.DisposalMode is FrameDisposalMode.Unspecified ? 0 : this.DisposalMode - 1);
        buffer[25] = (byte)this.BlendMode;
    }

    /// <summary>
    /// Parses the APngFrameControl from the given data buffer.
    /// </summary>
    /// <remarks>
    /// The dispose_op values 0, 1 and 2 map to <see cref="FrameDisposalMode.DoNotDispose"/>, <see cref="FrameDisposalMode.RestoreToBackground"/> and
    /// <see cref="FrameDisposalMode.RestoreToPrevious"/>. Older ImageSharp versions wrote dispose_op 255 for <see cref="FrameDisposalMode.Unspecified"/>, and
    /// decoded each value more than 2 as no disposal. Thus a value more than 2 maps to <see cref="FrameDisposalMode.DoNotDispose"/>, so the files of these
    /// versions decode with the same pixels.
    /// </remarks>
    /// <param name="data">The data to parse. It has at least <see cref="Size"/> bytes.</param>
    /// <returns>The parsed fcTL.</returns>
    public static FrameControl Parse(ReadOnlySpan<byte> data)
        => new(
            sequenceNumber: BinaryPrimitives.ReadUInt32BigEndian(data[..4]),
            width: BinaryPrimitives.ReadUInt32BigEndian(data[4..8]),
            height: BinaryPrimitives.ReadUInt32BigEndian(data[8..12]),
            xOffset: BinaryPrimitives.ReadUInt32BigEndian(data[12..16]),
            yOffset: BinaryPrimitives.ReadUInt32BigEndian(data[16..20]),
            delayNumerator: BinaryPrimitives.ReadUInt16BigEndian(data[20..22]),
            delayDenominator: BinaryPrimitives.ReadUInt16BigEndian(data[22..24]),
            disposalMode: data[24] <= 2 ? (FrameDisposalMode)(data[24] + 1) : FrameDisposalMode.DoNotDispose,
            blendMode: (FrameBlendMode)data[25]);
}
