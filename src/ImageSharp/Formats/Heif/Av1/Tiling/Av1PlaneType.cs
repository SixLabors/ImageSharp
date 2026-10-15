// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Identifies whether AV1 block processing targets the luma plane or either chroma plane.
/// </summary>
internal enum Av1PlaneType : int
{
    /// <summary>
    /// The luma plane.
    /// </summary>
    Y,

    /// <summary>
    /// Either chroma plane.
    /// </summary>
    Uv
}
