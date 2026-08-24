// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.ModeDecision;

/// <summary>
/// Identifies a predefined AV1 mode-decision geometry with a fixed superblock size, search depth, and partition set.
/// </summary>
internal enum Av1GeometryIndex
{
    /// <summary>
    /// The 64-pixel, four-depth geometry limited to square partitions.
    /// </summary>
    Geometry0,

    /// <summary>
    /// The 64-pixel, four-depth geometry with horizontal and vertical binary partitions down to 16 pixels.
    /// </summary>
    Geometry1,

    /// <summary>
    /// The 64-pixel, four-depth geometry with horizontal and vertical binary partitions down to 8 pixels.
    /// </summary>
    Geometry2,

    /// <summary>
    /// The 64-pixel, four-depth geometry with binary partitions at every supported size.
    /// </summary>
    Geometry3,

    /// <summary>
    /// The 64-pixel, five-depth geometry with binary partitions at every supported size.
    /// </summary>
    Geometry4,

    /// <summary>
    /// The 64-pixel, five-depth geometry that also enables four-way horizontal and vertical partitions.
    /// </summary>
    Geometry5,

    /// <summary>
    /// The 64-pixel, five-depth geometry that enables all supported partition shapes.
    /// </summary>
    Geometry6,

    /// <summary>
    /// The 128-pixel, six-depth geometry that enables all supported partition shapes.
    /// </summary>
    Geometry7,
}
