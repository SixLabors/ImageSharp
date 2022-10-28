// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// The base class for all image encoders.
/// </summary>
public abstract class ImageEncoder
{
    /// <summary>
    /// Gets a value indicating whether to ignore decoded metadata when encoding.
    /// </summary>
    public bool SkipMetadata { get; init; }

    /// <summary>
    /// Encodes the image to the specified stream from the <see cref="Image{TPixel}" />.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="Image{TPixel}" /> to encode from.</param>
    /// <param name="stream">The <see cref="Stream" /> to encode the image data to.</param>
    public abstract void Encode<TPixel>(Image<TPixel> image, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    /// Encodes the image to the specified stream from the <see cref="Image{TPixel}" />.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="Image{TPixel}" /> to encode from.</param>
    /// <param name="stream">The <see cref="Stream" /> to encode the image data to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task" /> representing the asynchronous operation.</returns>
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
