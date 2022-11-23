// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Extensions methods for <see cref="IImageDecoder"/> and <see cref="IImageDecoderSpecialized{T}"/>.
/// </summary>
public static class ImageDecoderExtensions
{
    /// <summary>
    /// Reads the raw image information from the specified stream.
    /// </summary>
    /// <param name="decoder">The decoder.</param>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <returns>The <see cref="IImageInfo"/> object.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public static IImageInfo Identify(this IImageDecoder decoder, DecoderOptions options, Stream stream)
        => Image.WithSeekableStream(
            options,
            stream,
            s => decoder.Identify(options, s, default));

    /// <summary>
    /// Reads the raw image information from the specified stream.
    /// </summary>
    /// <param name="decoder">The decoder.</param>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="Task{IImageInfo}"/> object.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public static Task<IImageInfo> IdentifyAsync(this IImageDecoder decoder, DecoderOptions options, Stream stream, CancellationToken cancellationToken = default)
        => Image.WithSeekableStreamAsync(
            options,
            stream,
            (s, ct) => decoder.Identify(options, s, ct),
            cancellationToken);

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="decoder">The decoder.</param>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <returns>The <see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public static Image<TPixel> Decode<TPixel>(this IImageDecoder decoder, DecoderOptions options, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
        => Image.WithSeekableStream(
            options,
            stream,
            s => decoder.Decode<TPixel>(options, s, default));

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image"/> of a specific pixel type.
    /// </summary>
    /// <param name="decoder">The decoder.</param>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <returns>The <see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public static Image Decode(this IImageDecoder decoder, DecoderOptions options, Stream stream)
        => Image.WithSeekableStream(
            options,
            stream,
            s => decoder.Decode(options, s, default));

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="decoder">The decoder.</param>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public static Task<Image<TPixel>> DecodeAsync<TPixel>(this IImageDecoder decoder, DecoderOptions options, Stream stream, CancellationToken cancellationToken = default)
        where TPixel : unmanaged, IPixel<TPixel>
        => Image.WithSeekableStreamAsync(
            options,
            stream,
            (s, ct) => decoder.Decode<TPixel>(options, s, ct),
            cancellationToken);

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image"/> of a specific pixel type.
    /// </summary>
    /// <param name="decoder">The decoder.</param>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public static Task<Image> DecodeAsync(this IImageDecoder decoder, DecoderOptions options, Stream stream, CancellationToken cancellationToken = default)
        => Image.WithSeekableStreamAsync(
            options,
            stream,
            (s, ct) => decoder.Decode(options, s, ct),
            cancellationToken);

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
    /// </summary>
    /// <typeparam name="T">The type of specialized options.</typeparam>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="decoder">The decoder.</param>
    /// <param name="options">The specialized decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <returns>The <see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public static Image<TPixel> Decode<T, TPixel>(this IImageDecoderSpecialized<T> decoder, T options, Stream stream)
        where T : ISpecializedDecoderOptions
        where TPixel : unmanaged, IPixel<TPixel>
        => Image.WithSeekableStream(
            options.GeneralOptions,
            stream,
            s => decoder.Decode<TPixel>(options, s, default));

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image"/> of a specific pixel type.
    /// </summary>
    /// <typeparam name="T">The type of specialized options.</typeparam>
    /// <param name="decoder">The decoder.</param>
    /// <param name="options">The specialized decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <returns>The <see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public static Image Decode<T>(this IImageDecoderSpecialized<T> decoder, T options, Stream stream)
        where T : ISpecializedDecoderOptions
        => Image.WithSeekableStream(
            options.GeneralOptions,
            stream,
            s => decoder.Decode(options, s, default));

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
    /// </summary>
    /// <typeparam name="T">The type of specialized options.</typeparam>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="decoder">The decoder.</param>
    /// <param name="options">The specialized decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public static Task<Image<TPixel>> DecodeAsync<T, TPixel>(this IImageDecoderSpecialized<T> decoder, T options, Stream stream, CancellationToken cancellationToken = default)
        where T : ISpecializedDecoderOptions
        where TPixel : unmanaged, IPixel<TPixel>
        => Image.WithSeekableStreamAsync(
            options.GeneralOptions,
            stream,
            (s, ct) => decoder.Decode<TPixel>(options, s, ct),
            cancellationToken);

    /// <summary>
    /// Decodes the image from the specified stream to an <see cref="Image"/> of a specific pixel type.
    /// </summary>
    /// <typeparam name="T">The type of specialized options.</typeparam>
    /// <param name="decoder">The decoder.</param>
    /// <param name="options">The specialized decoder options.</param>
    /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
    public static Task<Image> DecodeAsync<T>(this IImageDecoderSpecialized<T> decoder, T options, Stream stream, CancellationToken cancellationToken = default)
        where T : ISpecializedDecoderOptions
        => Image.WithSeekableStreamAsync(
            options.GeneralOptions,
            stream,
            (s, ct) => decoder.Decode(options, s, ct),
            cancellationToken);
}
