// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Identifies an AV1 luma or chroma sample plane.
/// </summary>
internal enum Av1Plane : int
{
    /// <summary>
    /// The luma plane.
    /// </summary>
    Y = 0,

    /// <summary>
    /// The first chroma plane.
    /// </summary>
    U = 1,

    /// <summary>
    /// The second chroma plane.
    /// </summary>
    V = 2,
}
