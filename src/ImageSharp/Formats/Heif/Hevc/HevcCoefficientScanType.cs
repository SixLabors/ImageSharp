// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Identifies the coefficient scan used by one HEVC transform block.
/// </summary>
internal enum HevcCoefficientScanType
{
    /// <summary>
    /// The up-right diagonal scan.
    /// </summary>
    Diagonal,

    /// <summary>
    /// The row-major horizontal scan.
    /// </summary>
    Horizontal,

    /// <summary>
    /// The column-major vertical scan.
    /// </summary>
    Vertical,
}
