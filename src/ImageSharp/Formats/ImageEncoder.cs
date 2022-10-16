// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// The base class for all image encoders.
/// </summary>
public abstract class ImageEncoder : IImageEncoder, IEncoderOptions
{
    /// <inheritdoc/>
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
public abstract class QuantizingImageEncoder : ImageEncoder, IQuantizingEncoderOptions
{
    /// <inheritdoc/>
    public IQuantizer Quantizer { get; init; } = KnownQuantizers.Octree;

    /// <inheritdoc/>
    public IPixelSamplingStrategy PixelSamplingStrategy { get; init; } = new DefaultPixelSamplingStrategy();
}
