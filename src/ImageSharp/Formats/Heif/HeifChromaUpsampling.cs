// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Specifies how subsampled chroma is expanded when decoding HEIF images.
/// </summary>
public enum HeifChromaUpsampling
{
    /// <summary>
    /// Uses nearest-neighbour sampling for 8-bit source samples and bilinear interpolation for higher bit depths.
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
