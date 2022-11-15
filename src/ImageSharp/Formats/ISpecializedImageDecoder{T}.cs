// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Defines the contract for an image decoder that supports specialized options.
/// </summary>
/// <typeparam name="T">The type of specialized options.</typeparam>
public interface ISpecializedImageDecoder<T> : IImageDecoder
    where T : ISpecializedDecoderOptions
{
    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="options">The specialized decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <returns>The <see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public Image<TPixel> Decode<TPixel>(T options, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image"/> of a specific pixel type.
    /// </summary>
    /// <param name="options">The specialized decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <returns>The <see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public Image Decode(T options, Stream stream);

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="options">The specialized decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public Task<Image<TPixel>> DecodeAsync<TPixel>(T options, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image"/> of a specific pixel type.
    /// </summary>
    /// <param name="options">The specialized decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public Task<Image> DecodeAsync(T options, Stream stream, CancellationToken cancellationToken);
}
