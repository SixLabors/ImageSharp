// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Acts as a base class for specialized image decoders.
/// Specialized decoders allow for additional options to be passed to the decoder.
/// Types that inherit this decoder are required to implement cancellable synchronous decoding operations only.
/// </summary>
/// <typeparam name="T">The type of specialized options.</typeparam>
public abstract class SpecializedImageDecoder<T> : ImageDecoder, ISpecializedImageDecoder<T>
    where T : ISpecializedDecoderOptions
{
    /// <inheritdoc/>
    public Image<TPixel> Decode<TPixel>(T options, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Image<TPixel> image = WithSeekableStream(
            options.GeneralOptions,
            stream,
            s => this.Decode<TPixel>(options, s, default));

        this.SetDecoderFormat(options.GeneralOptions.Configuration, image);

        return image;
    }

    /// <inheritdoc/>
    public Image Decode(T options, Stream stream)
    {
        Image image = WithSeekableStream(
            options.GeneralOptions,
            stream,
            s => this.Decode(options, s, default));

        this.SetDecoderFormat(options.GeneralOptions.Configuration, image);

        return image;
    }

    /// <inheritdoc/>
    public async Task<Image<TPixel>> DecodeAsync<TPixel>(T options, Stream stream, CancellationToken cancellationToken = default)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Image<TPixel> image = await WithSeekableMemoryStreamAsync(
            options.GeneralOptions,
            stream,
            (s, ct) => this.Decode<TPixel>(options, s, ct),
            cancellationToken).ConfigureAwait(false);

        this.SetDecoderFormat(options.GeneralOptions.Configuration, image);

        return image;
    }

    /// <inheritdoc/>
    public async Task<Image> DecodeAsync(T options, Stream stream, CancellationToken cancellationToken = default)
    {
        Image image = await WithSeekableMemoryStreamAsync(
            options.GeneralOptions,
            stream,
            (s, ct) => this.Decode(options, s, ct),
            cancellationToken).ConfigureAwait(false);

        this.SetDecoderFormat(options.GeneralOptions.Configuration, image);

        return image;
    }

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
    protected abstract Image<TPixel> Decode<TPixel>(T options, Stream stream, CancellationToken cancellationToken)
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
    protected abstract Image Decode(T options, Stream stream, CancellationToken cancellationToken);

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<TPixel>(this.CreateDefaultSpecializedOptions(options), stream, cancellationToken);

    /// <inheritdoc/>
    protected override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode(this.CreateDefaultSpecializedOptions(options), stream, cancellationToken);

    /// <summary>
    /// A factory method for creating the default specialized options.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <returns>The new <typeparamref name="T" />.</returns>
    protected abstract T CreateDefaultSpecializedOptions(DecoderOptions options);
}
