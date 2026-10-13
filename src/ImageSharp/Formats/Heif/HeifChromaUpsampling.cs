// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Specifies how the decoder expands subsampled chroma to the full image size.
/// </summary>
public enum HeifChromaUpsampling
{
    /// <summary>
    /// Uses nearest-neighbor sampling for 8-bit images and bilinear interpolation for higher bit depths.
    /// </summary>
    Auto,

    /// <summary>
    /// Repeats the nearest chroma sample.
    /// </summary>
    NearestNeighbor,

    /// <summary>
    /// Interpolates between neighboring chroma samples.
    /// </summary>
    Bilinear
}
