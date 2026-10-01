// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Specifies the quality measure that the AV1 encoder optimizes for.
/// </summary>
public enum HeifTuning
{
    /// <summary>
    /// Peak signal-to-noise ratio. The encoder keeps each pixel value near the source value.
    /// </summary>
    Psnr = 0,

    /// <summary>
    /// Structural similarity. The encoder keeps the local contrast and structure near the source.
    /// </summary>
    Ssim = 1,

    /// <summary>
    /// Perceived image quality. The encoder keeps fine detail and gives more data to flat areas.
    /// This setting also changes how <see cref="HeifEncoder.Quality"/> controls the compression.
    /// </summary>
    ImageQuality = 2,

    /// <summary>
    /// The SSIMULACRA 2 perceptual metric. The encoder uses the tools of <see cref="ImageQuality"/>, with settings
    /// for this metric. <see cref="HeifEncoder.Quality"/> controls the compression as it does for the other measures.
    /// </summary>
    Ssimulacra2 = 3
}
