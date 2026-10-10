// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Identifies the AV1 luma and chroma plane sampling layout.
/// </summary>
internal enum Av1ColorFormat
{
    /// <summary>
    /// Monochrome luma samples without chroma planes.
    /// </summary>
    Yuv400,

    /// <summary>
    /// Chroma samples subsampled by two horizontally and vertically.
    /// </summary>
    Yuv420,

    /// <summary>
    /// Chroma samples subsampled by two horizontally.
    /// </summary>
    Yuv422,

    /// <summary>
    /// Full-resolution luma and chroma samples.
    /// </summary>
    Yuv444,
}
