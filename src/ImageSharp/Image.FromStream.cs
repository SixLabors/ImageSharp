// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

/// <content>
/// Adds static methods allowing the creation of new image from a given stream.
/// </content>
public abstract partial class Image
{
    /// <summary>
    /// Detects the encoded image format type from the specified stream.
    /// </summary>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <returns>The <see cref="IImageFormat"/>.</returns>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static IImageFormat DetectFormat(Stream stream)
        => DetectFormat(DecoderOptions.Default, stream);

    /// <summary>
    /// Detects the encoded image format type from the specified stream.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <returns><see langword="true"/> if a match is found; otherwise, <see langword="false"/></returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static IImageFormat DetectFormat(DecoderOptions options, Stream stream)
        => WithSeekableStream(options, stream, s => InternalDetectFormat(options.Configuration, s));

    /// <summary>
    /// Detects the encoded image format type from the specified stream.
    /// </summary>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{IImageFormat}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Task<IImageFormat> DetectFormatAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => DetectFormatAsync(DecoderOptions.Default, stream, cancellationToken);

    /// <summary>
    /// Detects the encoded image format type from the specified stream.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{IImageFormat}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Task<IImageFormat> DetectFormatAsync(
        DecoderOptions options,
        Stream stream,
        CancellationToken cancellationToken = default)
        => WithSeekableStreamAsync(
            options,
            stream,
            (s, _) => Task.FromResult(InternalDetectFormat(options.Configuration, s)),
            cancellationToken);

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// </summary>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <returns><see langword="true"/> if the information can be read; otherwise, <see langword="false"/></returns>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static ImageInfo Identify(Stream stream)
        => Identify(DecoderOptions.Default, stream);

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the information from.</param>
    /// <returns>The <see cref="ImageInfo"/>.</returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static ImageInfo Identify(DecoderOptions options, Stream stream)
        => WithSeekableStream(options, stream, s => InternalIdentify(options, s));

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// </summary>
    /// <param name="stream">The image stream to read the information from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The <see cref="Task{ImageInfo}"/> representing the asynchronous operation.
    /// </returns>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Task<ImageInfo> IdentifyAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => IdentifyAsync(DecoderOptions.Default, stream, cancellationToken);

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the information from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The <see cref="Task{ImageInfo}"/> representing the asynchronous operation.
    /// </returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Task<ImageInfo> IdentifyAsync(
        DecoderOptions options,
        Stream stream,
        CancellationToken cancellationToken = default)
        => WithSeekableStreamAsync(
            options,
            stream,
            (s, ct) => InternalIdentifyAsync(options, s, ct),
            cancellationToken);

    /// <summary>
    /// Creates a new instance of the <see cref="Image"/> class from the given stream.
    /// The pixel format is automatically determined by the decoder.
    /// </summary>
    /// <param name="stream">The stream containing image information.</param>
    /// <returns><see cref="Image"/>.</returns>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Image Load(Stream stream)
        => Load(DecoderOptions.Default, stream);

    /// <summary>
    /// Creates a new instance of the <see cref="Image"/> class from the given stream.
    /// The pixel format is automatically determined by the decoder.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The stream containing image information.</param>
    /// <returns><see cref="Image"/>.</returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Image Load(DecoderOptions options, Stream stream)
        => WithSeekableStream(options, stream, s => Decode(options, s));

    /// <summary>
    /// Creates a new instance of the <see cref="Image"/> class from the given stream.
    /// The pixel format is automatically determined by the decoder.
    /// </summary>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Task<Image> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
        => LoadAsync(DecoderOptions.Default, stream, cancellationToken);

    /// <summary>
    /// Creates a new instance of the <see cref="Image"/> class from the given stream.
    /// The pixel format is automatically determined by the decoder.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Task<Image> LoadAsync(
        DecoderOptions options,
        Stream stream,
        CancellationToken cancellationToken = default)
        => WithSeekableStreamAsync(options, stream, (s, ct) => DecodeAsync(options, s, ct), cancellationToken);

    /// <summary>
    /// Creates a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The stream containing image information.</param>
    /// <returns><see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Image<TPixel> Load<TPixel>(Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
        => Load<TPixel>(DecoderOptions.Default, stream);

    /// <summary>
    /// Creates a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The stream containing image information.</param>
    /// <returns><see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Image<TPixel> Load<TPixel>(DecoderOptions options, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
        => WithSeekableStream(options, stream, s => Decode<TPixel>(options, s));

    /// <summary>
    /// Creates a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Task<Image<TPixel>> LoadAsync<TPixel>(Stream stream, CancellationToken cancellationToken = default)
        where TPixel : unmanaged, IPixel<TPixel>
        => LoadAsync<TPixel>(DecoderOptions.Default, stream, cancellationToken);

    /// <summary>
    /// Creates a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Task<Image<TPixel>> LoadAsync<TPixel>(
        DecoderOptions options,
        Stream stream,
        CancellationToken cancellationToken = default)
        where TPixel : unmanaged, IPixel<TPixel>
        => WithSeekableStreamAsync(options, stream, (s, ct) => DecodeAsync<TPixel>(options, s, ct), cancellationToken);

    /// <summary>
    /// Performs the given action against the stream ensuring that it is seekable.
    /// </summary>
    /// <typeparam name="T">The type of object returned from the action.</typeparam>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The input stream.</param>
    /// <param name="action">The action to perform.</param>
    /// <returns>The <typeparamref name="T"/>.</returns>
    /// <exception cref="NotSupportedException">Cannot read from the stream.</exception>
    internal static T WithSeekableStream<T>(
        DecoderOptions options,
        Stream stream,
        Func<Stream, T> action)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        if (!stream.CanRead)
        {
            throw new NotSupportedException("Cannot read from the stream.");
        }

        Configuration configuration = options.Configuration;
        if (stream.CanSeek)
        {
            if (configuration.ReadOrigin == ReadOrigin.Begin)
            {
                stream.Position = 0;
            }

            return action(stream);
        }

        using ChunkedMemoryStream memoryStream = new(configuration.MemoryAllocator);
        stream.CopyTo(memoryStream, configuration.StreamProcessingBufferSize);
        memoryStream.Position = 0;

        return action(memoryStream);
    }

    /// <summary>
    /// Performs the given action asynchronously against the stream ensuring that it is seekable.
    /// </summary>
    /// <typeparam name="T">The type of object returned from the action.</typeparam>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The input stream.</param>
    /// <param name="action">The action to perform.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The <see cref="Task{T}"/>.</returns>
    /// <exception cref="NotSupportedException">Cannot read from the stream.</exception>
    internal static async Task<T> WithSeekableStreamAsync<T>(
        DecoderOptions options,
        Stream stream,
        Func<Stream, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        if (!stream.CanRead)
        {
            throw new NotSupportedException("Cannot read from the stream.");
        }

        Configuration configuration = options.Configuration;
        if (stream.CanSeek)
        {
            if (configuration.ReadOrigin == ReadOrigin.Begin)
            {
                stream.Position = 0;
            }

            return await action(stream, cancellationToken).ConfigureAwait(false);
        }

        using ChunkedMemoryStream memoryStream = new(configuration.MemoryAllocator);
        await stream.CopyToAsync(memoryStream, configuration.StreamProcessingBufferSize, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;

        return await action(memoryStream, cancellationToken).ConfigureAwait(false);
    }
}
