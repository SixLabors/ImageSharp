// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal enum Av1PartitionType
{
    // See section 6.10.4 of Avi Spcification

    /// <summary>
    /// Not partitioned any further.
    /// </summary>
    None = 0,

    /// <summary>
    /// Horizontally split in 2 partitions.
    /// </summary>
    Horizontal = 1,

    /// <summary>
    /// Vertically split in 2 partitions.
    /// </summary>
    Vertical = 2,

    /// <summary>
    /// 4 equally sized partitions.
    /// </summary>
    Split = 3,

    /// <summary>
    /// Horizontal split and the top partition is split again.
    /// </summary>
    HorizontalA = 4,

    /// <summary>
    /// Horizontal split and the bottom partition is split again.
    /// </summary>
    HorizontalB = 5,

    /// <summary>
    /// Vertical split and the left partition is split again.
    /// </summary>
    VerticalA = 6,

    /// <summary>
    /// Vertical split and the right partitino is split again.
    /// </summary>
    VerticalB = 7,

    /// <summary>
    /// 4:1 horizontal partition.
    /// </summary>
    Horizontal4 = 8,

    /// <summary>
    /// 4:1 vertical partition.
    /// </summary>
    Vertical4 = 9,

    /// <summary>
    /// Invalid value.
    /// </summary>
    Invalid = 255
}
