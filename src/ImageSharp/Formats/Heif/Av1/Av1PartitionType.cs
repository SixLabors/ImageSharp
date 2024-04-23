// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal enum Av1PartitionType
{
    /// <summary>
    /// Not partitioned any further.
    /// </summary>
    None,

    /// <summary>
    /// Horizontally split in 2 partitions.
    /// </summary>
    Horizontal,

    /// <summary>
    /// Vertically split in 2 partitions.
    /// </summary>
    Vertical,

    /// <summary>
    /// 4 equally sized partitions.
    /// </summary>
    Split,

    /// <summary>
    /// Horizontal split and the top partition is split again.
    /// </summary>
    HorizontalA,

    /// <summary>
    /// Horizontal split and the bottom partition is split again.
    /// </summary>
    HorizontalB,

    /// <summary>
    /// Vertical split and the left partition is split again.
    /// </summary>
    VerticalA,

    /// <summary>
    /// Vertical split and the right partitino is split again.
    /// </summary>
    VerticalB,

    /// <summary>
    /// 4:1 horizontal partition.
    /// </summary>
    Horizontal4,

    /// <summary>
    /// 4:1 vertical partition.
    /// </summary>
    Vertical4,

    /// <summary>
    /// Invalid value.
    /// </summary>
    Invalid = 255
}
