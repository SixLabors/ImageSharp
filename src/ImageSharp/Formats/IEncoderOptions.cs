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
    /// Gets a value indicating whether to ignore decoded metadata when encoding.
    /// </summary>
    bool SkipMetadata { get; init; }
}

/// <summary>
/// Defines the contract for encoder options that allow color palette generation via quantization.
/// </summary>
public interface IQuantizingEncoderOptions : IEncoderOptions
{
    /// <summary>
    /// Gets the quantizer used to generate the color palette.
    /// </summary>
    IQuantizer Quantizer { get; init; }

    /// <summary>
    /// Gets the <see cref="IPixelSamplingStrategy"/> used for quantization when building a global color palette.
    /// </summary>
    IPixelSamplingStrategy GlobalPixelSamplingStrategy { get; init; }
}
