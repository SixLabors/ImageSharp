// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// The base class for all image decoders.
/// </summary>
public abstract class ImageDecoder
{
    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image{TPixel}" /> of a specific pixel type.
    /// </summary>
    /// <remarks>
    /// This method is designed to support the ImageSharp internal infrastructure and is not recommended for direct use.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream" /> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="Image{TPixel}" />.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    protected internal abstract Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image" />.
    /// </summary>
    /// <remarks>
    /// This method is designed to support the ImageSharp internal infrastructure and is not recommended for direct use.
    /// </remarks>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream" /> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="Image" />.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    protected internal abstract Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the raw image information from the specified stream.
    /// </summary>
    /// <remarks>
    /// This method is designed to support the ImageSharp internal infrastructure and is not recommended for direct use.
    /// </remarks>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="IImageInfo"/> object.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    protected internal abstract IImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken);

    /// <summary>
    /// Performs a resize operation against the decoded image. If the target size is not set, or the image size
    /// already matches the target size, the image is untouched.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    /// <param name="image">The decoded image.</param>
    protected static void Resize(DecoderOptions options, Image image)
    {
        if (ShouldResize(options, image))
        {
            ResizeOptions resizeOptions = new()
            {
                Size = options.TargetSize.Value,
                Sampler = options.Sampler,
                Mode = ResizeMode.Max
            };

            image.Mutate(x => x.Resize(resizeOptions));
        }
    }

    /// <summary>
    /// Determines whether the decoded image should be resized.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    /// <param name="image">The decoded image.</param>
    /// <returns><see langword="true"/> if the image should be resized, otherwise; <see langword="false"/>.</returns>
    private static bool ShouldResize(DecoderOptions options, Image image)
    {
        if (options.TargetSize is null)
        {
            return false;
        }

        Size targetSize = options.TargetSize.Value;
        Size currentSize = image.Size();
        return currentSize.Width != targetSize.Width && currentSize.Height != targetSize.Height;
    }
}

/// <summary>
/// The base class for all specialized image decoders.
/// Specialized decoders allow for additional options to be passed to the decoder.
/// </summary>
/// <typeparam name="T">The type of specialized options.</typeparam>
public abstract class SpecializedImageDecoder<T> : ImageDecoder
    where T : ISpecializedDecoderOptions
{
    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image{TPixel}" /> of a specific pixel type.
    /// </summary>
    /// <remarks>
    /// This method is designed to support the ImageSharp internal infrastructure and is not recommended for direct use.
    /// </remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="options">The specialized decoder options.</param>
    /// <param name="stream">The <see cref="Stream" /> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="Image{TPixel}" />.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    protected internal abstract Image<TPixel> Decode<TPixel>(T options, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image" /> of a specific pixel type.
    /// </summary>
    /// <remarks>
    /// This method is designed to support the ImageSharp internal infrastructure and is not recommended for direct use.
    /// </remarks>
    /// <param name="options">The specialized decoder options.</param>
    /// <param name="stream">The <see cref="Stream" /> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="Image{TPixel}" />.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    protected internal abstract Image Decode(T options, Stream stream, CancellationToken cancellationToken);

    /// <inheritdoc/>
    protected internal override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<TPixel>(this.CreateDefaultSpecializedOptions(options), stream, cancellationToken);

    /// <inheritdoc/>
    protected internal override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode(this.CreateDefaultSpecializedOptions(options), stream, cancellationToken);

    /// <summary>
    /// A factory method for creating the default specialized options.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <returns>The new <typeparamref name="T" />.</returns>
    protected internal abstract T CreateDefaultSpecializedOptions(DecoderOptions options);
}
