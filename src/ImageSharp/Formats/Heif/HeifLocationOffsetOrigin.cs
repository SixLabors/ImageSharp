// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Identifies the origin used to resolve an item-location extent offset.
/// </summary>
internal enum HeifLocationOffsetOrigin
{
    /// <summary>
    /// The base and extent offsets are absolute file offsets.
    /// </summary>
    FileOffset = 0,

    /// <summary>
    /// The base and extent offsets are relative to the item-data box payload.
    /// </summary>
    ItemDataOffset = 1,

    /// <summary>
    /// The base and extent offsets are relative to another item payload.
    /// </summary>
    ItemOffset = 2
}
