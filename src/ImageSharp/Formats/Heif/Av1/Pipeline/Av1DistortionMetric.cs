// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The distortion metric of rate-distortion decisions. Reference: aom_dist_metric.
/// </summary>
internal enum Av1DistortionMetric
{
    /// <summary>
    /// The plain squared error. Reference: AOM_DIST_METRIC_PSNR.
    /// </summary>
    Psnr,

    /// <summary>
    /// The squared error weighted by the quantization matrices. Reference: AOM_DIST_METRIC_QM_PSNR.
    /// </summary>
    QuantizationMatrixPsnr,
}
