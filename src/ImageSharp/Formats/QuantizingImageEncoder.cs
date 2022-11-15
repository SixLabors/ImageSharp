// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Acts as a base class for all image encoders that allow color palette generation via quantization.
/// </summary>
public abstract class QuantizingImageEncoder : SynchronousImageEncoder
{
    /// <summary>
    /// Gets the quantizer used to generate the color palette.
    /// </summary>
    public IQuantizer Quantizer { get; init; } = KnownQuantizers.Octree;

    /// <summary>
    /// Gets the <see cref="IPixelSamplingStrategy"/> used for quantization when building color palettes.
    /// </summary>
    public IPixelSamplingStrategy PixelSamplingStrategy { get; init; } = new DefaultPixelSamplingStrategy();
}
