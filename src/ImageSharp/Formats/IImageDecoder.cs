// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Defines the contract for all image decoders.
/// </summary>
public interface IImageDecoder
{
    /// <summary>
    /// Reads the raw image information from the specified stream.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <returns>The <see cref="IImageInfo"/> object.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public IImageInfo Identify(DecoderOptions options, Stream stream);

    /// <summary>
    /// Reads the raw image information from the specified stream.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="Task{IImageInfo}"/> object.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public Task<IImageInfo> IdentifyAsync(DecoderOptions options, Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <returns>The <see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image"/> of a specific pixel type.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <returns>The <see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public Image Decode(DecoderOptions options, Stream stream);

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public Task<Image<TPixel>> DecodeAsync<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken = default)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image"/> of a specific pixel type.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public Task<Image> DecodeAsync(DecoderOptions options, Stream stream, CancellationToken cancellationToken = default);
}
