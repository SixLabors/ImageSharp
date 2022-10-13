// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Defines the contract for basic encoder options.
/// </summary>
public interface IEncoderOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to ignore decoded metadata when encoding.
    /// </summary>
    bool SkipMetadata { get; set; }
}

/// <summary>
/// Defines the contract for encoder options that allow color palette generation via quantization.
/// </summary>
public interface IQuantizingEncoderOptions : IEncoderOptions
{
    /// <summary>
    /// Gets or sets the quantizer used to generate the color palette.
    /// </summary>
    IQuantizer Quantizer { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="IPixelSamplingStrategy"/> used for quantization when building a global color palette.
    /// </summary>
    IPixelSamplingStrategy GlobalPixelSamplingStrategy { get; set; }
}
