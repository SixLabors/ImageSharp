// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Specifies the number of tile rows or columns in a frame. More tiles let a parallel decoder work faster but
/// compress less well.
/// </summary>
public enum HeifTileCount
{
    /// <summary>
    /// One tile. The default setting.
    /// </summary>
    One = 1,

    /// <summary>
    /// Two tiles.
    /// </summary>
    Two = 2,

    /// <summary>
    /// Four tiles.
    /// </summary>
    Four = 4,

    /// <summary>
    /// Eight tiles.
    /// </summary>
    Eight = 8,

    /// <summary>
    /// Sixteen tiles.
    /// </summary>
    Sixteen = 16,

    /// <summary>
    /// Thirty-two tiles.
    /// </summary>
    ThirtyTwo = 32,

    /// <summary>
    /// Sixty-four tiles.
    /// </summary>
    SixtyFour = 64
}
