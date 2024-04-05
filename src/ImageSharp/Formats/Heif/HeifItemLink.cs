// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Link between <see cref="HeifItem"/> instances within the same HEIF file.
/// </summary>
internal class HeifItemLink(Heif4CharCode type, uint sourceId)
{
    /// <summary>
    /// Gets the type of link.
    /// </summary>
    public Heif4CharCode Type { get; } = type;

    /// <summary>
    /// Gets the ID of the source item of this link.
    /// </summary>
    public uint SourceId { get; } = sourceId;

    /// <summary>
    /// Gets the destination item IDs of this link.
    /// </summary>
    public List<uint> DestinationIds { get; } = new List<uint>();
}
