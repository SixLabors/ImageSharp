// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
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
    /// By reading the header on the provided stream this calculates the images format type.
    /// </summary>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <param name="format">The format type or null if none found.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <returns>returns true when format was detected otherwise false.</returns>
    public static bool TryDetectFormat(Stream stream, [NotNullWhen(true)] out IImageFormat? format)
        => TryDetectFormat(DecoderOptions.Default, stream, out format);

    /// <summary>
    /// By reading the header on the provided stream this calculates the images format type.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <param name="format">The format type or null if none found.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <returns>returns true when format was detected otherwise false.</returns>
    public static bool TryDetectFormat(DecoderOptions options, Stream stream, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = WithSeekableStream(options, stream, s => InternalDetectFormat(options.Configuration, s));
        return format != null;
    }

    /// <summary>
    /// By reading the header on the provided stream this calculates the images format type.
    /// </summary>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <returns>A <see cref="Task{IImageFormat}"/> representing the asynchronous operation or null if none is found.</returns>
    public static Task<IImageFormat> DetectFormatAsync(Stream stream, CancellationToken cancellationToken = default)
        => DetectFormatAsync(DecoderOptions.Default, stream, cancellationToken);

    /// <summary>
    /// By reading the header on the provided stream this calculates the images format type.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <returns>A <see cref="Task{IImageFormat}"/> representing the asynchronous operation.</returns>
    public static Task<IImageFormat> DetectFormatAsync(DecoderOptions options, Stream stream, CancellationToken cancellationToken = default)
        => WithSeekableStreamAsync(
            options,
            stream,
            (s, _) => Task.FromResult(InternalDetectFormat(options.Configuration, s)),
            cancellationToken);

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// </summary>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>
    /// The <see cref="IImageInfo"/> or null if a suitable info detector is not found.
    /// </returns>
    public static IImageInfo Identify(Stream stream)
        => Identify(stream, out IImageFormat _);

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// </summary>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>
    /// A <see cref="Task{IImageInfo}"/> representing the asynchronous operation or null if
    /// a suitable detector is not found.
    /// </returns>
    public static Task<IImageInfo> IdentifyAsync(Stream stream, CancellationToken cancellationToken = default)
        => IdentifyAsync(DecoderOptions.Default, stream, cancellationToken);

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// </summary>
    /// <param name="stream">The image stream to read the header from.</param>
    /// <param name="format">The format type of the decoded image.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>
    /// The <see cref="IImageInfo"/> or null if a suitable info detector is not found.
    /// </returns>
    public static IImageInfo Identify(Stream stream, out IImageFormat format)
        => Identify(DecoderOptions.Default, stream, out format);

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the information from.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>
    /// The <see cref="IImageInfo"/> or null if a suitable info detector is not found.
    /// </returns>
    public static IImageInfo Identify(DecoderOptions options, Stream stream)
        => Identify(options, stream, out _);

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the information from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>
    /// A <see cref="Task{IImageInfo}"/> representing the asynchronous operation or null if
    /// a suitable detector is not found.
    /// </returns>
    public static async Task<IImageInfo> IdentifyAsync(
        DecoderOptions options,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        (IImageInfo ImageInfo, IImageFormat Format) res = await IdentifyWithFormatAsync(options, stream, cancellationToken).ConfigureAwait(false);
        return res.ImageInfo;
    }

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the information from.</param>
    /// <param name="format">The format type of the decoded image.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>
    /// The <see cref="IImageInfo"/> or null if a suitable info detector is not found.
    /// </returns>
    public static IImageInfo Identify(DecoderOptions options, Stream stream, out IImageFormat format)
    {
        (IImageInfo ImageInfo, IImageFormat Format) data = WithSeekableStream(options, stream, s => InternalIdentify(options, s));

        format = data.Format;
        return data.ImageInfo;
    }

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// </summary>
    /// <param name="stream">The image stream to read the information from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>
    /// The <see cref="Task{ValueTuple}"/> representing the asynchronous operation with the parameter type
    /// <see cref="IImageInfo"/> property set to null if suitable info detector is not found.
    /// </returns>
    public static Task<(IImageInfo ImageInfo, IImageFormat Format)> IdentifyWithFormatAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
        => IdentifyWithFormatAsync(DecoderOptions.Default, stream, cancellationToken);

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The image stream to read the information from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>
    /// The <see cref="Task{ValueTuple}"/> representing the asynchronous operation with the parameter type
    /// <see cref="IImageInfo"/> property set to null if suitable info detector is not found.
    /// </returns>
    public static Task<(IImageInfo ImageInfo, IImageFormat Format)> IdentifyWithFormatAsync(
        DecoderOptions options,
        Stream stream,
        CancellationToken cancellationToken = default)
        => WithSeekableStreamAsync(
            options,
            stream,
            (s, ct) => InternalIdentifyAsync(options, s, ct),
            cancellationToken);

    /// <summary>
    /// Decode a new instance of the <see cref="Image"/> class from the given stream.
    /// The pixel format is selected by the decoder.
    /// </summary>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="format">The format type of the decoded image.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>The <see cref="Image"/>.</returns>
    public static Image Load(Stream stream, out IImageFormat format)
        => Load(DecoderOptions.Default, stream, out format);

    /// <summary>
    /// Decode a new instance of the <see cref="Image"/> class from the given stream.
    /// The pixel format is selected by the decoder.
    /// </summary>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>A <see cref="Task{ValueTuple}"/> representing the asynchronous operation.</returns>
    public static Task<(Image Image, IImageFormat Format)> LoadWithFormatAsync(Stream stream, CancellationToken cancellationToken = default)
        => LoadWithFormatAsync(DecoderOptions.Default, stream, cancellationToken);

    /// <summary>
    /// Decode a new instance of the <see cref="Image"/> class from the given stream.
    /// The pixel format is selected by the decoder.
    /// </summary>
    /// <param name="stream">The stream containing image information.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>The <see cref="Image"/>.</returns>
    public static Image Load(Stream stream) => Load(DecoderOptions.Default, stream);

    /// <summary>
    /// Decode a new instance of the <see cref="Image"/> class from the given stream.
    /// The pixel format is selected by the decoder.
    /// </summary>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    public static Task<Image> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
        => LoadAsync(DecoderOptions.Default, stream, cancellationToken);

    /// <summary>
    /// Decode a new instance of the <see cref="Image"/> class from the given stream.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The stream containing image information.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>A new <see cref="Image"/>.</returns>
    public static Image Load(DecoderOptions options, Stream stream)
        => Load(options, stream, out _);

    /// <summary>
    /// Decode a new instance of the <see cref="Image"/> class from the given stream.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    public static async Task<Image> LoadAsync(DecoderOptions options, Stream stream, CancellationToken cancellationToken = default)
        => (await LoadWithFormatAsync(options, stream, cancellationToken).ConfigureAwait(false)).Image;

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
    /// </summary>
    /// <param name="stream">The stream containing image information.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> Load<TPixel>(Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
        => Load<TPixel>(DecoderOptions.Default, stream);

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
    /// </summary>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    public static Task<Image<TPixel>> LoadAsync<TPixel>(Stream stream, CancellationToken cancellationToken = default)
        where TPixel : unmanaged, IPixel<TPixel>
        => LoadAsync<TPixel>(DecoderOptions.Default, stream, cancellationToken);

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
    /// </summary>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="format">The format type of the decoded image.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> Load<TPixel>(Stream stream, out IImageFormat format)
        where TPixel : unmanaged, IPixel<TPixel>
        => Load<TPixel>(DecoderOptions.Default, stream, out format);

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
    /// </summary>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A <see cref="Task{ValueTuple}"/> representing the asynchronous operation.</returns>
    public static Task<(Image<TPixel> Image, IImageFormat Format)> LoadWithFormatAsync<TPixel>(Stream stream, CancellationToken cancellationToken = default)
        where TPixel : unmanaged, IPixel<TPixel>
        => LoadWithFormatAsync<TPixel>(DecoderOptions.Default, stream, cancellationToken);

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The stream containing image information.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> Load<TPixel>(DecoderOptions options, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
        => Load<TPixel>(options, stream, out IImageFormat _);

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="format">The format type of the decoded image.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static Image<TPixel> Load<TPixel>(DecoderOptions options, Stream stream, out IImageFormat format)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        (Image<TPixel> Image, IImageFormat Format) data = WithSeekableStream(options, stream, s => Decode<TPixel>(options, s));

        format = data.Format;

        if (data.Image is null)
        {
            ThrowNotLoaded(options);
        }

        return data.Image;
    }

    /// <summary>
    /// Create a new instance of the <see cref="Image"/> class from the given stream.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>A <see cref="Task{ValueTuple}"/> representing the asynchronous operation.</returns>
    public static async Task<(Image Image, IImageFormat Format)> LoadWithFormatAsync(
        DecoderOptions options,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        (Image Image, IImageFormat Format) data =
            await WithSeekableStreamAsync(options, stream, (s, ct) => DecodeAsync(options, s, ct), cancellationToken)
                 .ConfigureAwait(false);

        if (data.Image is null)
        {
            ThrowNotLoaded(options);
        }

        return data;
    }

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A <see cref="Task{ValueTuple}"/> representing the asynchronous operation.</returns>
    public static async Task<(Image<TPixel> Image, IImageFormat Format)> LoadWithFormatAsync<TPixel>(
        DecoderOptions options,
        Stream stream,
        CancellationToken cancellationToken = default)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        (Image<TPixel> Image, IImageFormat Format) data =
            await WithSeekableStreamAsync(options, stream, (s, ct) => DecodeAsync<TPixel>(options, s, ct), cancellationToken)
                 .ConfigureAwait(false);

        if (data.Image is null)
        {
            ThrowNotLoaded(options);
        }

        return data;
    }

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<Image<TPixel>> LoadAsync<TPixel>(
        DecoderOptions options,
        Stream stream,
        CancellationToken cancellationToken = default)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        (Image<TPixel> img, _) = await LoadWithFormatAsync<TPixel>(options, stream, cancellationToken)
                                      .ConfigureAwait(false);
        return img;
    }

    /// <summary>
    /// Decode a new instance of the <see cref="Image"/> class from the given stream.
    /// The pixel format is selected by the decoder.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="stream">The stream containing image information.</param>
    /// <param name="format">The format type of the decoded image.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The stream is null.</exception>
    /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image Load(DecoderOptions options, Stream stream, out IImageFormat format)
    {
        (Image img, IImageFormat fmt) = WithSeekableStream(options, stream, s => Decode(options, s));

        format = fmt;

        if (img is null)
        {
            ThrowNotLoaded(options);
        }

        return img;
    }

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

    [DoesNotReturn]
    private static void ThrowNotLoaded(DecoderOptions options)
    {
        StringBuilder sb = new();
        sb.AppendLine("Image cannot be loaded. Available decoders:");

        foreach (KeyValuePair<IImageFormat, IImageDecoder> val in options.Configuration.ImageFormatsManager.ImageDecoders)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, " - {0} : {1}{2}", val.Key.Name, val.Value.GetType().Name, Environment.NewLine);
        }

        throw new UnknownImageFormatException(sb.ToString());
    }
}
