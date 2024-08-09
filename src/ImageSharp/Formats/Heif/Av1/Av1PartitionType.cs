// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal enum Av1PartitionType
{
    // See section 6.10.4 of Avi Spcification

    /// <summary>
    /// Not partitioned any further.
    /// </summary>
    /// <remarks>
    /// <code>
    /// ***
    /// * *
    /// ***
    /// </code>
    /// </remarks>
    None = 0,

    /// <summary>
    /// Horizontally split in 2 partitions.
    /// </summary>
    /// <remarks>
    /// <code>
    /// ***
    /// * *
    /// ***
    /// * *
    /// ***
    /// </code>
    /// </remarks>
    Horizontal = 1,

    /// <summary>
    /// Vertically split in 2 partitions.
    /// </summary>
    /// <remarks>
    /// <code>
    /// *****
    /// * * *
    /// *****
    /// </code>
    /// </remarks>
    Vertical = 2,

    /// <summary>
    /// 4 equally sized partitions.
    /// </summary>
    /// <remarks>
    /// <code>
    /// *****
    /// * * *
    /// *****
    /// * * *
    /// *****
    /// </code>
    /// </remarks>
    Split = 3,

    /// <summary>
    /// Horizontal split and the top partition is split again.
    /// </summary>
    /// <remarks>
    /// <code>
    /// *****
    /// * * *
    /// *****
    /// *   *
    /// *****
    /// </code>
    /// </remarks>
    HorizontalA = 4,

    /// <summary>
    /// Horizontal split and the bottom partition is split again.
    /// </summary>
    /// <remarks>
    /// <code>
    /// *****
    /// *   *
    /// *****
    /// * * *
    /// *****
    /// </code>
    /// </remarks>
    HorizontalB = 5,

    /// <summary>
    /// Vertical split and the left partition is split again.
    /// </summary>
    /// <remarks>
    /// <code>
    /// *****
    /// * * *
    /// *** *
    /// * * *
    /// *****
    /// </code>
    /// </remarks>
    VerticalA = 6,

    /// <summary>
    /// Vertical split and the right partition is split again.
    /// </summary>
    /// <remarks>
    /// <code>
    /// *****
    /// * * *
    /// * ***
    /// * * *
    /// *****
    /// </code>
    /// </remarks>
    VerticalB = 7,

    /// <summary>
    /// 4:1 horizontal partition.
    /// </summary>
    /// <remarks>
    /// <code>
    /// ***
    /// * *
    /// ***
    /// * *
    /// ***
    /// * *
    /// ***
    /// * *
    /// ***
    /// </code>
    /// </remarks>
    Horizontal4 = 8,

    /// <summary>
    /// 4:1 vertical partition.
    /// </summary>
    /// <remarks>
    /// <code>
    /// *********
    /// * * * * *
    /// *********
    /// </code>
    /// </remarks>
    Vertical4 = 9,

    /// <summary>
    /// Invalid value.
    /// </summary>
    Invalid = 255
}
