// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Png.Chunks;

internal readonly struct AnimationControl
{
    public const int Size = 8;

    public AnimationControl(uint numberFrames, uint numberPlays)
    {
        this.NumberFrames = numberFrames;
        this.NumberPlays = numberPlays;
    }

    /// <summary>
    /// Gets the number of frames
    /// </summary>
    public uint NumberFrames { get; }

    /// <summary>
    /// Gets the number of times to loop this APNG.  0 indicates infinite looping.
    /// </summary>
    public uint NumberPlays { get; }

    /// <summary>
    /// Writes the acTL to the given buffer.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    public void WriteTo(Span<byte> buffer)
    {
        BinaryPrimitives.WriteInt32BigEndian(buffer[..4], (int)this.NumberFrames);
        BinaryPrimitives.WriteInt32BigEndian(buffer[4..8], (int)this.NumberPlays);
    }

    /// <summary>
    /// Parses the APngAnimationControl from the given data buffer.
    /// </summary>
    /// <param name="data">The data to parse.</param>
    /// <returns>The parsed acTL.</returns>
    public static AnimationControl Parse(ReadOnlySpan<byte> data)
        => new(
            numberFrames: BinaryPrimitives.ReadUInt32BigEndian(data[..4]),
            numberPlays: BinaryPrimitives.ReadUInt32BigEndian(data[4..8]));
}
