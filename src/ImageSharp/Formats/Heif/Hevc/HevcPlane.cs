// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Identifies an HEVC luma or chroma reconstruction plane.
/// </summary>
internal enum HevcPlane
{
    /// <summary>
    /// The luma or first separate-color plane.
    /// </summary>
    Y = 0,

    /// <summary>
    /// The blue-difference chroma or second separate-color plane.
    /// </summary>
    Cb = 1,

    /// <summary>
    /// The red-difference chroma or third separate-color plane.
    /// </summary>
    Cr = 2,
}
