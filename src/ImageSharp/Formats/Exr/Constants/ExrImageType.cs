// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Exr.Constants;

/// <summary>
/// Enum for the differnt exr image type.
/// </summary>
internal enum ExrImageType
{
    /// <summary>
    /// The image data is stored in scan lines.
    /// </summary>
    ScanLine = 0,

    /// <summary>
    /// The image data is stored in tile.
    /// This is not yet supported.
    /// </summary>
    Tiled = 1
}
