// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// The base class for all image encoders.
/// </summary>
public abstract class ImageEncoder : IImageEncoder
{
    /// <summary>
    /// Gets a value indicating whether to ignore decoded metadata when encoding.
    /// </summary>
    public bool SkipMetadata { get; init; }

    /// <inheritdoc/>
    public abstract void Encode<TPixel>(Image<TPixel> image, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <inheritdoc/>
    public abstract Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>;
}

/// <summary>
/// The base class for all image encoders that allow color palette generation via quantization.
/// </summary>
public abstract class QuantizingImageEncoder : ImageEncoder
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
