// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Enumerates the chroma sampling layouts supported for HEIF image encoding.
/// </summary>
public enum HeifChromaSubsampling : byte
{
    /// <summary>
    /// A single luminance plane without chroma planes.
    /// </summary>
    Monochrome,

    /// <summary>
    /// Chroma sampled at half the luma resolution horizontally and vertically.
    /// </summary>
    Yuv420,

    /// <summary>
    /// Chroma sampled at half the luma resolution horizontally and full resolution vertically.
    /// </summary>
    Yuv422,

    /// <summary>
    /// Chroma sampled at full luma resolution horizontally and vertically.
    /// </summary>
    Yuv444
}
