// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The distortion metric of rate-distortion decisions.
/// </summary>
internal enum Av1DistortionMetric
{
    /// <summary>
    /// The plain squared error.
    /// </summary>
    Psnr,

    /// <summary>
    /// The squared error weighted by the quantization matrices.
    /// </summary>
    QuantizationMatrixPsnr,
}
