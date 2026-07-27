// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Represents the data stored in an ANI "anih" chunk.
/// </summary>
internal struct AniHeader
{
    /// <summary>
    /// The number of bytes in the ANI header.
    /// </summary>
    public const int Size = 9 * sizeof(uint);

    /// <summary>
    /// Gets or sets the declared ANI header size.
    /// </summary>
    public uint BytesInHeader { get; set; }

    /// <summary>
    /// Gets or sets the number of embedded frame resources.
    /// </summary>
    public uint FrameCount { get; set; }

    /// <summary>
    /// Gets or sets the number of animation steps.
    /// </summary>
    public uint StepCount { get; set; }

    /// <summary>
    /// Gets or sets the frame width used by bitmap-based animations.
    /// </summary>
    public uint Width { get; set; }

    /// <summary>
    /// Gets or sets the frame height used by bitmap-based animations.
    /// </summary>
    public uint Height { get; set; }

    /// <summary>
    /// Gets or sets the encoded bits per pixel.
    /// </summary>
    public uint BitCount { get; set; }

    /// <summary>
    /// Gets or sets the number of color planes.
    /// </summary>
    public uint Planes { get; set; }

    /// <summary>
    /// Gets or sets the default display rate in sixtieths of a second.
    /// </summary>
    public uint DisplayRate { get; set; }

    /// <summary>
    /// Gets or sets the ANI header flags.
    /// </summary>
    public AniHeaderFlags Flags { get; set; }

    /// <summary>
    /// Parses an ANI header from its little-endian byte representation.
    /// </summary>
    /// <param name="data">The ANI header data.</param>
    /// <returns>The parsed ANI header.</returns>
    public static AniHeader Parse(ReadOnlySpan<byte> data)
        => new()
        {
            BytesInHeader = BinaryPrimitives.ReadUInt32LittleEndian(data),
            FrameCount = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]),
            StepCount = BinaryPrimitives.ReadUInt32LittleEndian(data[8..]),
            Width = BinaryPrimitives.ReadUInt32LittleEndian(data[12..]),
            Height = BinaryPrimitives.ReadUInt32LittleEndian(data[16..]),
            BitCount = BinaryPrimitives.ReadUInt32LittleEndian(data[20..]),
            Planes = BinaryPrimitives.ReadUInt32LittleEndian(data[24..]),
            DisplayRate = BinaryPrimitives.ReadUInt32LittleEndian(data[28..]),
            Flags = (AniHeaderFlags)BinaryPrimitives.ReadUInt32LittleEndian(data[32..])
        };

    /// <summary>
    /// Writes the ANI header to its little-endian byte representation.
    /// </summary>
    /// <param name="destination">The destination buffer.</param>
    public readonly void WriteTo(Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination, this.BytesInHeader);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], this.FrameCount);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], this.StepCount);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[12..], this.Width);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], this.Height);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[20..], this.BitCount);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[24..], this.Planes);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[28..], this.DisplayRate);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[32..], (uint)this.Flags);
    }
}
