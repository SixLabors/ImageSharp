// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Identifies the differential pulse-code modulation applied to an HEVC residual block.
/// </summary>
internal enum HevcResidualDpcmMode : byte
{
    /// <summary>
    /// No residual differential pulse-code modulation is applied.
    /// </summary>
    None = 0,

    /// <summary>
    /// Residual differences accumulate from left to right within each row.
    /// </summary>
    Horizontal = 1,

    /// <summary>
    /// Residual differences accumulate from top to bottom within each column.
    /// </summary>
    Vertical = 2,
}
