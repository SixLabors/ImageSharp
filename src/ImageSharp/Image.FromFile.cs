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
    /// <param name="path">The image file to open and to read the header from.</param>
    /// <param name="format">
    /// When this method returns, contains the format that matches the given file;
    /// otherwise, the default value for the type of the <paramref name="format"/> parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if a match is found; otherwise, <see langword="false"/></returns>
    public static bool TryDetectFormat(string path, [NotNullWhen(true)] out IImageFormat? format)
        => TryDetectFormat(DecoderOptions.Default, path, out format);

    /// <summary>
    /// Detects the encoded image format type from the specified file.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="path">The image file to open and to read the header from.</param>
    /// <param name="format">
    /// When this method returns, contains the format that matches the given file;
    /// otherwise, the default value for the type of the <paramref name="format"/> parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if a match is found; otherwise, <see langword="false"/></returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    public static bool TryDetectFormat(DecoderOptions options, string path, [NotNullWhen(true)] out IImageFormat? format)
    {
        Guard.NotNull(options, nameof(options));

        using Stream file = options.Configuration.FileSystem.OpenRead(path);
        return TryDetectFormat(options, file, out format);
    }

    /// <summary>
    /// Detects the encoded image format type from the specified file.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="path">The image file to open and to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Attempt}"/> representing the asynchronous operation.</returns>
    public static Task<Attempt<IImageFormat>> TryDetectFormatAsync(
        string path,
        CancellationToken cancellationToken = default)
        => TryDetectFormatAsync(DecoderOptions.Default, path, cancellationToken);

    /// <summary>
    /// Detects the encoded image format type from the specified file.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="path">The image file to open and to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <returns>A <see cref="Task{Attempt}"/> representing the asynchronous operation.</returns>
    public static async Task<Attempt<IImageFormat>> TryDetectFormatAsync(
        DecoderOptions options,
        string path,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(options, nameof(options));

        using Stream stream = options.Configuration.FileSystem.OpenRead(path);
        return await TryDetectFormatAsync(options, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the raw image information from the specified file path without fully decoding it.
    /// A return value indicates whether the operation succeeded.
    /// </summary>
    /// <param name="path">The image file to open and to read the header from.</param>
    /// <returns><see langword="true"/> if the information can be read; otherwise, <see langword="false"/></returns>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="NotSupportedException">The file stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static ImageInfo Identify(string path)
        => Identify(DecoderOptions.Default, path);

    /// <summary>
    /// Reads the raw image information from the specified file path without fully decoding it.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="path">The image file to open and to read the header from.</param>
    /// <returns>The <see cref="ImageInfo"/>.</returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="NotSupportedException">The file stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static ImageInfo Identify(DecoderOptions options, string path)
    {
        Guard.NotNull(options, nameof(options));

        using Stream stream = options.Configuration.FileSystem.OpenRead(path);
        return Identify(options, stream);
    }

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// </summary>
    /// <param name="path">The image file to open and to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <returns>
    /// The <see cref="Task{ImageInfo}"/> representing the asynchronous operation.
    /// </returns>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="NotSupportedException">The file stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Task<ImageInfo> IdentifyAsync(string path, CancellationToken cancellationToken = default)
        => IdentifyAsync(DecoderOptions.Default, path, cancellationToken);

    /// <summary>
    /// Reads the raw image information from the specified stream without fully decoding it.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="path">The image file to open and to read the header from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The <see cref="Task{ImageInfo}"/> representing the asynchronous operation.
    /// </returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="NotSupportedException">The file stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static async Task<ImageInfo> IdentifyAsync(
        DecoderOptions options,
        string path,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(options, nameof(options));
        using Stream stream = options.Configuration.FileSystem.OpenRead(path);
        return await IdentifyAsync(options, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Image"/> class from the given file path.
    /// The pixel format is automatically determined by the decoder.
    /// </summary>
    /// <param name="path">The file path to the image.</param>
    /// <returns><see cref="Image"/>.</returns>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="NotSupportedException">The file stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Image Load(string path)
        => Load(DecoderOptions.Default, path);

    /// <summary>
    /// Creates a new instance of the <see cref="Image"/> class from the given file path.
    /// The pixel format is automatically determined by the decoder.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="path">The file path to the image.</param>
    /// <returns><see cref="Image"/>.</returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="NotSupportedException">The file stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Image Load(DecoderOptions options, string path)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(path, nameof(path));

        using Stream stream = options.Configuration.FileSystem.OpenRead(path);
        return Load(options, stream);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Image"/> class from the given file path.
    /// The pixel format is automatically determined by the decoder.
    /// </summary>
    /// <param name="path">The file path to the image.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="NotSupportedException">The file stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Task<Image> LoadAsync(string path, CancellationToken cancellationToken = default)
        => LoadAsync(DecoderOptions.Default, path, cancellationToken);

    /// <summary>
    /// Creates a new instance of the <see cref="Image"/> class from the given file path.
    /// The pixel format is automatically determined by the decoder.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    /// <param name="path">The file path to the image.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="NotSupportedException">The file stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static async Task<Image> LoadAsync(
        DecoderOptions options,
        string path,
        CancellationToken cancellationToken = default)
    {
        using Stream stream = options.Configuration.FileSystem.OpenRead(path);
        return await LoadAsync(options, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Image{TPixel}"/> class from the given file path.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="path">The file path to the image.</param>
    /// <returns><see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="NotSupportedException">The file stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Image<TPixel> Load<TPixel>(string path)
        where TPixel : unmanaged, IPixel<TPixel>
        => Load<TPixel>(DecoderOptions.Default, path);

    /// <summary>
    /// Creates a new instance of the <see cref="Image{TPixel}"/> class from the given file path.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="options">The general decoder options.</param>
    /// <param name="path">The file path to the image.</param>
    /// <returns><see cref="Image{TPixel}"/>.</returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="NotSupportedException">The file stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Image<TPixel> Load<TPixel>(DecoderOptions options, string path)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(path, nameof(path));

        using Stream stream = options.Configuration.FileSystem.OpenRead(path);
        return Load<TPixel>(options, stream);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="Image{TPixel}"/> class from the given file path.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="path">The file path to the image.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="NotSupportedException">The file stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static Task<Image<TPixel>> LoadAsync<TPixel>(string path, CancellationToken cancellationToken = default)
        where TPixel : unmanaged, IPixel<TPixel>
        => LoadAsync<TPixel>(DecoderOptions.Default, path, cancellationToken);

    /// <summary>
    /// Creates a new instance of the <see cref="Image{TPixel}"/> class from the given file path.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="options">The general decoder options.</param>
    /// <param name="path">The file path to the image.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentNullException">The path is null.</exception>
    /// <exception cref="NotSupportedException">The file stream is not readable or the image format is not supported.</exception>
    /// <exception cref="InvalidImageContentException">The encoded image contains invalid content.</exception>
    /// <exception cref="UnknownImageFormatException">The encoded image format is unknown.</exception>
    public static async Task<Image<TPixel>> LoadAsync<TPixel>(
        DecoderOptions options,
        string path,
        CancellationToken cancellationToken = default)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(path, nameof(path));

        using Stream stream = options.Configuration.FileSystem.OpenRead(path);
        return await LoadAsync<TPixel>(options, stream, cancellationToken).ConfigureAwait(false);
    }
}
