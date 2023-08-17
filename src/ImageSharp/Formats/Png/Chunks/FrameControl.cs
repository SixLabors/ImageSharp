// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Png.Chunks;

internal readonly struct FrameControl
{
    public const int Size = 26;

    public FrameControl(
        int sequenceNumber,
        int width,
        int height,
        int xOffset,
        int yOffset,
        short delayNumber,
        short delayDenominator,
        PngDisposeOperation disposeOperation,
        PngBlendOperation blendOperation)
    {
        this.SequenceNumber = sequenceNumber;
        this.Width = width;
        this.Height = height;
        this.XOffset = xOffset;
        this.YOffset = yOffset;
        this.DelayNumber = delayNumber;
        this.DelayDenominator = delayDenominator;
        this.DisposeOperation = disposeOperation;
        this.BlendOperation = blendOperation;
    }

    /// <summary>
    /// Gets the sequence number of the animation chunk, starting from 0
    /// </summary>
    public int SequenceNumber { get; }

    /// <summary>
    /// Gets the width of the following frame
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the following frame
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the X position at which to render the following frame
    /// </summary>
    public int XOffset { get; }

    /// <summary>
    /// Gets the Y position at which to render the following frame
    /// </summary>
    public int YOffset { get; }

    /// <summary>
    /// Gets the X limit at which to render the following frame
    /// </summary>
    public uint XLimit => (uint)(this.XOffset + this.Width);

    /// <summary>
    /// Gets the Y limit at which to render the following frame
    /// </summary>
    public uint YLimit => (uint)(this.YOffset + this.Height);

    /// <summary>
    /// Gets the frame delay fraction numerator
    /// </summary>
    public short DelayNumber { get; }

    /// <summary>
    /// Gets the frame delay fraction denominator
    /// </summary>
    public short DelayDenominator { get; }

    /// <summary>
    /// Gets the type of frame area disposal to be done after rendering this frame
    /// </summary>
    public PngDisposeOperation DisposeOperation { get; }

    /// <summary>
    /// Gets the type of frame area rendering for this frame
    /// </summary>
    public PngBlendOperation BlendOperation { get; }

    /// <summary>
    /// Validates the APng fcTL.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Thrown if the image does pass validation.
    /// </exception>
    public void Validate(PngHeader hdr)
    {
        if (this.XOffset < 0)
        {
            PngThrowHelper.ThrowInvalidParameter(this.XOffset, "Expected >= 0");
        }

        if (this.YOffset < 0)
        {
            PngThrowHelper.ThrowInvalidParameter(this.YOffset, "Expected >= 0");
        }

        if (this.Width <= 0)
        {
            PngThrowHelper.ThrowInvalidParameter(this.Width, "Expected > 0");
        }

        if (this.Height <= 0)
        {
            PngThrowHelper.ThrowInvalidParameter(this.Height, "Expected > 0");
        }

        if (this.XLimit > hdr.Width)
        {
            PngThrowHelper.ThrowInvalidParameter(this.XOffset, this.Width, $"The sum of them > {nameof(PngHeader)}.{nameof(PngHeader.Width)}");
        }

        if (this.YLimit > hdr.Height)
        {
            PngThrowHelper.ThrowInvalidParameter(this.YOffset, this.Height, $"The sum of them > {nameof(PngHeader)}.{nameof(PngHeader.Height)}");
        }
    }

    /// <summary>
    /// Parses the APngFrameControl from the given metadata.
    /// </summary>
    /// <param name="frameMetadata">The metadata to parse.</param>
    /// <param name="sequenceNumber">Sequence number.</param>
    public static FrameControl FromMetadata(PngFrameMetadata frameMetadata, int sequenceNumber)
    {
        FrameControl fcTL = new(
            sequenceNumber,
            frameMetadata.Width,
            frameMetadata.Height,
            frameMetadata.XOffset,
            frameMetadata.YOffset,
            frameMetadata.DelayNumber,
            frameMetadata.DelayDenominator,
            frameMetadata.DisposeOperation,
            frameMetadata.BlendOperation);
        return fcTL;
    }

    /// <summary>
    /// Writes the fcTL to the given buffer.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    public void WriteTo(Span<byte> buffer)
    {
        BinaryPrimitives.WriteInt32BigEndian(buffer[..4], this.SequenceNumber);
        BinaryPrimitives.WriteInt32BigEndian(buffer[4..8], this.Width);
        BinaryPrimitives.WriteInt32BigEndian(buffer[8..12], this.Height);
        BinaryPrimitives.WriteInt32BigEndian(buffer[12..16], this.XOffset);
        BinaryPrimitives.WriteInt32BigEndian(buffer[16..20], this.YOffset);
        BinaryPrimitives.WriteInt16BigEndian(buffer[20..22], this.DelayNumber);
        BinaryPrimitives.WriteInt16BigEndian(buffer[22..24], this.DelayDenominator);

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
            sequenceNumber: BinaryPrimitives.ReadInt32BigEndian(data[..4]),
            width: BinaryPrimitives.ReadInt32BigEndian(data[4..8]),
            height: BinaryPrimitives.ReadInt32BigEndian(data[8..12]),
            xOffset: BinaryPrimitives.ReadInt32BigEndian(data[12..16]),
            yOffset: BinaryPrimitives.ReadInt32BigEndian(data[16..20]),
            delayNumber: BinaryPrimitives.ReadInt16BigEndian(data[20..22]),
            delayDenominator: BinaryPrimitives.ReadInt16BigEndian(data[22..24]),
            disposeOperation: (PngDisposeOperation)data[24],
            blendOperation: (PngBlendOperation)data[25]);
}
