// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Represents a RIFF chunk identifier and payload size.
/// </summary>
internal struct AniRiffChunkHeader
{
    /// <summary>
    /// Gets or sets the chunk identifier.
    /// </summary>
    public uint FourCc { get; set; }

    /// <summary>
    /// Gets or sets the chunk payload size in bytes, excluding alignment padding.
    /// </summary>
    public uint Size { get; set; }

    /// <summary>
    /// Parses a RIFF chunk header from its little-endian byte representation.
    /// </summary>
    /// <param name="data">The RIFF chunk header data.</param>
    /// <returns>The parsed RIFF chunk header.</returns>
    public static AniRiffChunkHeader Parse(ReadOnlySpan<byte> data)
        => new()
        {
            FourCc = BinaryPrimitives.ReadUInt32LittleEndian(data),
            Size = BinaryPrimitives.ReadUInt32LittleEndian(data[4..])
        };
}
