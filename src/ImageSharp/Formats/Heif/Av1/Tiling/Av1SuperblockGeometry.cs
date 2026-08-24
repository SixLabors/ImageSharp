// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Describes a superblock node's location, dimensions, and children in the encoder partition tree.
/// </summary>
internal class Av1SuperblockGeometry
{
    /// <summary>
    /// Gets or sets a value indicating whether the superblock lies completely within the coded frame.
    /// </summary>
    public bool IsComplete { get; internal set; }
}
