// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The perceptual metric the encoder tunes its decisions for.
/// </summary>
internal enum Av1Tuning
{
    /// <summary>
    /// Peak signal-to-noise ratio. This is the default tune.
    /// </summary>
    Psnr,

    /// <summary>
    /// Structural similarity.
    /// </summary>
    Ssim,

    /// <summary>
    /// Image quality, tuned for still images.
    /// </summary>
    Iq,

    /// <summary>
    /// The SSIMULACRA 2 metric. It uses the image quality tools with its own luma quantization matrices and a larger 4:2:0 chroma quantizer decrease.
    /// </summary>
    Ssimulacra2,
}
