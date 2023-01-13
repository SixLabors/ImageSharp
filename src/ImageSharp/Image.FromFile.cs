// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

/// <content>
/// Adds static methods allowing the creation of new image from a given file.
/// </content>
public abstract partial class Image
{
    /// <summary>
    /// Detects the encoded image format type from the specified file.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="filePath">The image file to open and to read the header from.</param>
    /// <param name="format">
    /// When this method returns, contains the format that matches the given file;
    /// otherwise, the default value for the type of the <paramref name="format"/> parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if a match is found; otherwise, <see langword="false"/></returns>
    public static bool TryDetectFormat(string filePath, [NotNullWhen(true)] out IImageFormat? format)
        => TryDetectFormat(DecoderOptions.Default, filePath, out format);

    /// <summary>
    /// Detects the encoded image format type from the specified file.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="filePath">The image file to open and to read the header from.</param>
    /// <param name="format">
    /// When this method returns, contains the format that matches the given file;
    /// otherwise, the default value for the type of the <paramref name="format"/> parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if a match is found; otherwise, <see langword="false"/></returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    public static bool TryDetectFormat(DecoderOptions options, string filePath, [NotNullWhen(true)] out IImageFormat? format)
    {
        Guard.NotNull(options, nameof(options));

        using Stream file = options.Configuration.FileSystem.OpenRead(filePath);
        return TryDetectFormat(options, file, out format);
    }

    /// <summary>
    /// Detects the encoded image format type from the specified file.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="filePath">The image file to open and to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Attempt}"/> representing the asynchronous operation.</returns>
    public static Task<Attempt<IImageFormat>> TryDetectFormatAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => TryDetectFormatAsync(DecoderOptions.Default, filePath, cancellationToken);

    /// <summary>
    /// Detects the encoded image format type from the specified file.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="filePath">The image file to open and to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <returns>A <see cref="Task{Attempt}"/> representing the asynchronous operation.</returns>
    public static async Task<Attempt<IImageFormat>> TryDetectFormatAsync(
        DecoderOptions options,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(options, nameof(options));

        using Stream stream = options.Configuration.FileSystem.OpenRead(filePath);
        return await TryDetectFormatAsync(options, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the raw image information from the specified file path without fully decoding it.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="filePath">The image file to open and to read the header from.</param>
    /// <param name="info">
    /// When this method returns, contains the raw image information;
    /// otherwise, the default value for the type of the <paramref name="info"/> parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if the information can be read; otherwise, <see langword="false"/></returns>
    public static bool TryIdentify(string filePath, [NotNullWhen(true)] out ImageInfo? info)
        => TryIdentify(DecoderOptions.Default, filePath, out info);

    /// <summary>
    /// Reads the raw image information from the specified file path without fully decoding it.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="filePath">The image file to open and to read the header from.</param>
    /// <param name="info">
    /// When this method returns, contains the raw image information;
    /// otherwise, the default value for the type of the <paramref name="info"/> parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if the information can be read; otherwise, <see langword="false"/></returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    public static bool TryIdentify(DecoderOptions options, string filePath, [NotNullWhen(true)] out ImageInfo? info)
    {
        Guard.NotNull(options, nameof(options));

        using Stream stream = options.Configuration.FileSystem.OpenRead(filePath);
        return TryIdentify(options, stream, out info);
    }

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="filePath">The image file to open and to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <returns>
    /// The <see cref="Task{Attempt}"/> representing the asynchronous operation.
    /// </returns>
    public static Task<Attempt<ImageInfo>> TryIdentifyAsync(
        string filePath,
        CancellationToken cancellationToken = default)
        => TryIdentifyAsync(DecoderOptions.Default, filePath, cancellationToken);

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="filePath">The image file to open and to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <returns>
    /// The <see cref="Task{Attempt}"/> representing the asynchronous operation.
    /// </returns>
    public static async Task<Attempt<ImageInfo>> TryIdentifyAsync(
        DecoderOptions options,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(options, nameof(options));
        using Stream stream = options.Configuration.FileSystem.OpenRead(filePath);
        return await TryIdentifyAsync(options, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Create a new instance of the <see cref="Image"/> class from the given file.
    /// </summary>
    /// <param name="path">The file path to the image.</param>
    /// <exception cref="NotSupportedException">
    /// Thrown if the stream is not readable nor seekable.
    /// </exception>
    /// <returns>The <see cref="Image"/>.</returns>
    public static Image Load(string path)
        => Load(DecoderOptions.Default, path);

    /// <summary>
    /// Create a new instance of the <see cref="Image"/> class from the given file.
    /// </summary>
    /// <param name="path">The file path to the image.</param>
    /// <param name="format">The mime type of the decoded image.</param>
    /// <exception cref="NotSupportedException">
    /// Thrown if the stream is not readable nor seekable.
    /// </exception>
    /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
    public static Image Load(string path, out IImageFormat format)
        => Load(DecoderOptions.Default, path, out format);

    /// <summary>
    /// Create a new instance of the <see cref="Image"/> class from the given file.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="path">The file path to the image.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="NotSupportedException">Image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>The <see cref="Image"/>.</returns>
    public static Image Load(DecoderOptions options, string path)
        => Load(options, path, out _);

    /// <summary>
    /// Create a new instance of the <see cref="Image"/> class from the given file.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="path">The file path to the image.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="NotSupportedException">Image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    public static async Task<Image> LoadAsync(
        DecoderOptions options,
        string path,
        CancellationToken cancellationToken = default)
    {
        using Stream stream = options.Configuration.FileSystem.OpenRead(path);
        (Image img, _) = await LoadWithFormatAsync(options, stream, cancellationToken)
            .ConfigureAwait(false);
        return img;
    }

    /// <summary>
    /// Create a new instance of the <see cref="Image"/> class from the given file.
    /// </summary>
    /// <param name="path">The file path to the image.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="ArgumentNullException">The decoder is null.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="NotSupportedException">Image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    public static Task<Image> LoadAsync(string path, CancellationToken cancellationToken = default)
        => LoadAsync(DecoderOptions.Default, path, cancellationToken);

    /// <summary>
    /// Create a new instance of the <see cref="Image"/> class from the given file.
    /// </summary>
    /// <param name="path">The file path to the image.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <exception cref="NotSupportedException">Image format is not supported.</exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    public static Task<Image<TPixel>> LoadAsync<TPixel>(string path, CancellationToken cancellationToken = default)
        where TPixel : unmanaged, IPixel<TPixel>
        => LoadAsync<TPixel>(DecoderOptions.Default, path, cancellationToken);

    /// <summary>
    /// Create a new instance of the <see cref="Image"/> class from the given file.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="path">The file path to the image.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="NotSupportedException">Image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    public static async Task<Image<TPixel>> LoadAsync<TPixel>(
        DecoderOptions options,
        string path,
        CancellationToken cancellationToken = default)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(options, nameof(options));

        using Stream stream = options.Configuration.FileSystem.OpenRead(path);
        (Image<TPixel> img, _) =
            await LoadWithFormatAsync<TPixel>(options, stream, cancellationToken).ConfigureAwait(false);
        return img;
    }

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
    /// </summary>
    /// <param name="path">The file path to the image.</param>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <exception cref="NotSupportedException">Image format is not supported.</exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> Load<TPixel>(string path)
        where TPixel : unmanaged, IPixel<TPixel>
        => Load<TPixel>(DecoderOptions.Default, path);

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
    /// </summary>
    /// <param name="path">The file path to the image.</param>
    /// <param name="format">The mime type of the decoded image.</param>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <exception cref="NotSupportedException">Image format is not supported.</exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> Load<TPixel>(string path, out IImageFormat format)
        where TPixel : unmanaged, IPixel<TPixel>
        => Load<TPixel>(DecoderOptions.Default, path, out format);

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="path">The file path to the image.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="NotSupportedException">Image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> Load<TPixel>(DecoderOptions options, string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(path, nameof(path));

        using Stream stream = options.Configuration.FileSystem.OpenRead(path);
        return Load<TPixel>(options, stream);
    }

    /// <summary>
    /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="path">The file path to the image.</param>
    /// <param name="format">The mime type of the decoded image.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="NotSupportedException">Image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image<TPixel> Load<TPixel>(DecoderOptions options, string path, out IImageFormat format)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(path, nameof(path));

        using Stream stream = options.Configuration.FileSystem.OpenRead(path);
        return Load<TPixel>(options, stream, out format);
    }

    /// <summary>
    /// Create a new instance of the <see cref="Image"/> class from the given file.
    /// The pixel type is selected by the decoder.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="path">The file path to the image.</param>
    /// <param name="format">The mime type of the decoded image.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
    /// <exception cref="NotSupportedException">Image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
    /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
    public static Image Load(DecoderOptions options, string path, out IImageFormat format)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(path, nameof(path));

        using Stream stream = options.Configuration.FileSystem.OpenRead(path);
        return Load(options, stream, out format);
    }
}
