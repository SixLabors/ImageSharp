// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Link between <see cref="HeicItem"/> instances within the same HEIC file.
/// </summary>
public class HeicItemLink(Heic4CharCode type, uint sourceId)
{
    /// <summary>
    /// Gets the type of link.
    /// </summary>
    public Heic4CharCode Type { get; } = type;

    /// <summary>
    /// Gets the ID of the source item of this link.
    /// </summary>
    public uint SourceId { get; } = sourceId;

    /// <summary>
    /// Gets the destination item IDs of this link.
    /// </summary>
    public List<uint> DestinationIds { get; } = new List<uint>();
}
