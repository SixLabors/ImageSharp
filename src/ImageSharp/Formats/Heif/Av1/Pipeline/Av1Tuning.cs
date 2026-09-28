// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The perceptual metric the encoder tunes its decisions for. Reference: aom_tune_metric.
/// </summary>
internal enum Av1Tuning
{
    /// <summary>
    /// Peak signal-to-noise ratio, the libaom default. Reference: AOM_TUNE_PSNR.
    /// </summary>
    Psnr,

    /// <summary>
    /// Structural similarity. Reference: AOM_TUNE_SSIM.
    /// </summary>
    Ssim,

    /// <summary>
    /// Image quality, tuned for still images. Reference: AOM_TUNE_IQ.
    /// </summary>
    Iq,
}
