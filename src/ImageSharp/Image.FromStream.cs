// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
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
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <param name="format">
    /// When this method returns, contains the format that matches the given stream;
    /// otherwise, the default value for the type of the <paramref name="format"/> parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if a match is found; otherwise, <see langword="false"/></returns>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    public static bool TryDetectFormat(Stream stream, [NotNullWhen(true)] out IImageFormat? format)
        => TryDetectFormat(DecoderOptions.Default, stream, out format);

    /// <summary>
    /// Detects the encoded image format type from the specified stream.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <param name="format">
    /// When this method returns, contains the format that matches the given stream;
    /// otherwise, the default value for the type of the <paramref name="format"/> parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if a match is found; otherwise, <see langword="false"/></returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    public static bool TryDetectFormat(DecoderOptions options, Stream stream, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = WithSeekableStream(options, stream, s => InternalDetectFormat(options.Configuration, s));
        return format is not null;
    }

    /// <summary>
    /// Detects the encoded image format type from the specified stream.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <returns>A <see cref="Task{Attempt}"/> representing the asynchronous operation.</returns>
    public static Task<Attempt<IImageFormat>> TryDetectFormatAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => TryDetectFormatAsync(DecoderOptions.Default, stream, cancellationToken);

    /// <summary>
    /// Detects the encoded image format type from the specified stream.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <returns>A <see cref="Task{Attempt}"/> representing the asynchronous operation.</returns>
    public static async Task<Attempt<IImageFormat>> TryDetectFormatAsync(
        DecoderOptions options,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        IImageFormat? format = await WithSeekableStreamAsync(
            options,
            stream,
            (s, _) => Task.FromResult(InternalDetectFormat(options.Configuration, s)),
            cancellationToken).ConfigureAwait(false);

        return new() { Value = format };
    }

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <param name="info">
    /// When this method returns, contains the raw image information;
    /// otherwise, the default value for the type of the <paramref name="info"/> parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if the information can be read; otherwise, <see langword="false"/></returns>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    public static bool TryIdentify(Stream stream, [NotNullWhen(true)] out ImageInfo? info)
        => TryIdentify(DecoderOptions.Default, stream, out info);

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the information from.</param>
    /// <param name="info">
    /// When this method returns, contains the raw image information;
    /// otherwise, the default value for the type of the <paramref name="info"/> parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if the information can be read; otherwise, <see langword="false"/></returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    public static bool TryIdentify(DecoderOptions options, Stream stream, [NotNullWhen(true)] out ImageInfo? info)
    {
        info = WithSeekableStream(options, stream, s => InternalIdentify(options, s));
        return info is not null;
    }

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="stream">The image stream to read the information from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>
    /// The <see cref="Task{Attempt}"/> representing the asynchronous operation.
    /// </returns>
    public static Task<Attempt<ImageInfo>> TryIdentifyAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => TryIdentifyAsync(DecoderOptions.Default, stream, cancellationToken);

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the information from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>
    /// The <see cref="Task{Attempt}"/> representing the asynchronous operation.
    /// </returns>
    public static async Task<Attempt<ImageInfo>> TryIdentifyAsync(
        DecoderOptions options,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ImageInfo? info = await WithSeekableStreamAsync(
            options,
            stream,
            (s, ct) => InternalIdentifyAsync(options, s, ct),
            cancellationToken).ConfigureAwait(false);

        return new() { Value = info };
    }

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
