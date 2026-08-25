// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Identifies the location of a 4:2:0 chroma sample relative to its associated two-by-two luma sample region.
/// </summary>
internal enum HevcChromaSampleLocation : byte
{
    /// <summary>
    /// The chroma sample is horizontally co-sited with the left luma column and vertically centered.
    /// </summary>
    Left = 0,

    /// <summary>
    /// The chroma sample is horizontally and vertically centered.
    /// </summary>
    Center = 1,

    /// <summary>
    /// The chroma sample is co-sited with the top-left luma sample.
    /// </summary>
    TopLeft = 2,

    /// <summary>
    /// The chroma sample is horizontally centered and co-sited with the top luma row.
    /// </summary>
    Top = 3,

    /// <summary>
    /// The chroma sample is horizontally co-sited with the left luma column and vertically co-sited with the bottom luma row.
    /// </summary>
    BottomLeft = 4,

    /// <summary>
    /// The chroma sample is horizontally centered and vertically co-sited with the bottom luma row.
    /// </summary>
    Bottom = 5,
}
