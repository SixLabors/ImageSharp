// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#nullable disable
using System.Buffers;
using SixLabors.ImageSharp.Formats.Png;

namespace SixLabors.ImageSharp.Formats.Ani;

internal readonly struct AniChunk
{
    public AniChunk(int length, AniChunkType type, IMemoryOwner<byte> data = null)
    {
        this.Length = length;
        this.Type = type;
        this.Data = data;
    }

    /// <summary>
    /// Gets the length.
    /// An unsigned integer giving the number of bytes in the chunk's
    /// data field. The length counts only the data field, not itself,
    /// the chunk type code, or the CRC. Zero is a valid length
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the chunk type.
    /// The value is the equal to the UInt32BigEndian encoding of its 4 ASCII characters.
    /// </summary>
    public AniChunkType Type { get; }

    /// <summary>
    /// Gets the data bytes appropriate to the chunk type, if any.
    /// This field can be of zero length or null.
    /// </summary>
    public IMemoryOwner<byte> Data { get; }
}
