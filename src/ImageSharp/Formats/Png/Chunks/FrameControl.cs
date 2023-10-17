// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Png.Chunks;

internal readonly struct FrameControl
{
    public const int Size = 26;

    public FrameControl(
        uint sequenceNumber,
        uint width,
        uint height,
        uint xOffset,
        uint yOffset,
        ushort delayNumerator,
        ushort delayDenominator,
        PngDisposalMethod disposeOperation,
        PngBlendMethod blendOperation)
    {
        this.SequenceNumber = sequenceNumber;
        this.Width = width;
        this.Height = height;
        this.XOffset = xOffset;
        this.YOffset = yOffset;
        this.DelayNumerator = delayNumerator;
        this.DelayDenominator = delayDenominator;
        this.DisposeOperation = disposeOperation;
        this.BlendOperation = blendOperation;
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
    public PngDisposalMethod DisposeOperation { get; }

    /// <summary>
    /// Gets the type of frame area rendering for this frame
    /// </summary>
    public PngBlendMethod BlendOperation { get; }

    /// <summary>
    /// Validates the APng fcTL.
    /// </summary>
    /// <param name="header">The header.</param>
    /// <exception cref="NotSupportedException">
    /// Thrown if the image does pass validation.
    /// </exception>
    public void Validate(PngHeader header)
    {
        if (this.Width == 0)
        {
            PngThrowHelper.ThrowInvalidParameter(this.Width, "Expected > 0");
        }

        if (this.Height == 0)
        {
            PngThrowHelper.ThrowInvalidParameter(this.Height, "Expected > 0");
        }

        if (this.XMax > header.Width)
        {
            PngThrowHelper.ThrowInvalidParameter(this.XOffset, this.Width, $"The x-offset plus width > {nameof(PngHeader)}.{nameof(PngHeader.Width)}");
        }

        if (this.YMax > header.Height)
        {
            PngThrowHelper.ThrowInvalidParameter(this.YOffset, this.Height, $"The y-offset plus height > {nameof(PngHeader)}.{nameof(PngHeader.Height)}");
        }
    }

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

        buffer[24] = (byte)this.DisposeOperation;
        buffer[25] = (byte)this.BlendOperation;
    }

    /// <summary>
    /// Parses the APngFrameControl from the given data buffer.
    /// </summary>
    /// <param name="data">The data to parse.</param>
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
            disposeOperation: (PngDisposalMethod)data[24],
            blendOperation: (PngBlendMethod)data[25]);
}
