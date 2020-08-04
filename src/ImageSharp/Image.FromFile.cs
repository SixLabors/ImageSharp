// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <content>
    /// Adds static methods allowing the creation of new image from a given file.
    /// </content>
    public abstract partial class Image
    {
        /// <summary>
        /// By reading the header on the provided file this calculates the images mime type.
        /// </summary>
        /// <param name="filePath">The image file to open and to read the header from.</param>
        /// <returns>The mime type or null if none found.</returns>
        public static IImageFormat DetectFormat(string filePath)
            => DetectFormat(Configuration.Default, filePath);

        /// <summary>
        /// By reading the header on the provided file this calculates the images mime type.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="filePath">The image file to open and to read the header from.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <returns>The mime type or null if none found.</returns>
        public static IImageFormat DetectFormat(Configuration configuration, string filePath)
        {
            Guard.NotNull(configuration, nameof(configuration));

            using (Stream file = configuration.FileSystem.OpenRead(filePath))
            {
                return DetectFormat(configuration, file);
            }
        }

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="filePath">The image file to open and to read the header from.</param>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if suitable info detector not found.
        /// </returns>
        public static IImageInfo Identify(string filePath)
            => Identify(filePath, out IImageFormat _);

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="filePath">The image file to open and to read the header from.</param>
        /// <param name="format">The format type of the decoded image.</param>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if suitable info detector not found.
        /// </returns>
        public static IImageInfo Identify(string filePath, out IImageFormat format)
            => Identify(Configuration.Default, filePath, out format);

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="filePath">The image file to open and to read the header from.</param>
        /// <param name="format">The format type of the decoded image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if suitable info detector is not found.
        /// </returns>
        public static IImageInfo Identify(Configuration configuration, string filePath, out IImageFormat format)
        {
            Guard.NotNull(configuration, nameof(configuration));
            using (Stream file = configuration.FileSystem.OpenRead(filePath))
            {
                return Identify(configuration, file, out format);
            }
        }

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="filePath">The image file to open and to read the header from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <returns>
        /// The <see cref="Task{ValueTuple}"/> representing the asynchronous operation with the parameter type
        /// <see cref="IImageInfo"/> property set to null if suitable info detector is not found.
        /// </returns>
        public static Task<IImageInfo> IdentifyAsync(string filePath, CancellationToken cancellationToken = default)
            => IdentifyAsync(Configuration.Default, filePath, cancellationToken);

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="filePath">The image file to open and to read the header from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <returns>
        /// The <see cref="Task{ValueTuple}"/> representing the asynchronous operation with the parameter type
        /// <see cref="IImageInfo"/> property set to null if suitable info detector is not found.
        /// </returns>
        public static async Task<IImageInfo> IdentifyAsync(
            Configuration configuration,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            (IImageInfo ImageInfo, IImageFormat Format) res = await IdentifyWithFormatAsync(configuration, filePath, cancellationToken)
                    .ConfigureAwait(false);
            return res.ImageInfo;
        }

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="filePath">The image file to open and to read the header from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <returns>
        /// The <see cref="Task{ValueTuple}"/> representing the asynchronous operation with the parameter type
        /// <see cref="IImageInfo"/> property set to null if suitable info detector is not found.
        /// </returns>
        public static Task<(IImageInfo ImageInfo, IImageFormat Format)> IdentifyWithFormatAsync(
            string filePath,
            CancellationToken cancellationToken = default)
            => IdentifyWithFormatAsync(Configuration.Default, filePath, cancellationToken);

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="filePath">The image file to open and to read the header from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <returns>
        /// The <see cref="Task{ValueTuple}"/> representing the asynchronous operation with the parameter type
        /// <see cref="IImageInfo"/> property set to null if suitable info detector is not found.
        /// </returns>
        public static async Task<(IImageInfo ImageInfo, IImageFormat Format)> IdentifyWithFormatAsync(
            Configuration configuration,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            Guard.NotNull(configuration, nameof(configuration));
            using Stream stream = configuration.FileSystem.OpenRead(filePath);
            return await IdentifyWithFormatAsync(configuration, stream, cancellationToken)
                .ConfigureAwait(false);
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
            => Load(Configuration.Default, path);

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
            => Load(Configuration.Default, path, out format);

        /// <summary>
        /// Create a new instance of the <see cref="Image"/> class from the given file.
        /// </summary>
        /// <param name="configuration">The configuration for the decoder.</param>
        /// <param name="path">The file path to the image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(Configuration configuration, string path)
            => Load(configuration, path, out _);

        /// <summary>
        /// Create a new instance of the <see cref="Image"/> class from the given file.
        /// </summary>
        /// <param name="configuration">The configuration for the decoder.</param>
        /// <param name="path">The file path to the image.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
        public static async Task<Image> LoadAsync(
            Configuration configuration,
            string path,
            CancellationToken cancellationToken = default)
        {
            using Stream stream = configuration.FileSystem.OpenRead(path);
            (Image img, _) = await LoadWithFormatAsync(configuration, stream, cancellationToken)
                .ConfigureAwait(false);
            return img;
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image"/> class from the given file.
        /// </summary>
        /// <param name="configuration">The Configuration.</param>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="ArgumentNullException">The decoder is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(Configuration configuration, string path, IImageDecoder decoder)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(path, nameof(path));

            using (Stream stream = configuration.FileSystem.OpenRead(path))
            {
                return Load(configuration, stream, decoder);
            }
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
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
        public static Task<Image> LoadAsync(string path, CancellationToken cancellationToken = default)
            => LoadAsync(Configuration.Default, path, cancellationToken);

        /// <summary>
        /// Create a new instance of the <see cref="Image"/> class from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="ArgumentNullException">The decoder is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
        public static Task<Image> LoadAsync(string path, IImageDecoder decoder)
            => LoadAsync(Configuration.Default, path, decoder, default);

        /// <summary>
        /// Create a new instance of the <see cref="Image"/> class from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="ArgumentNullException">The decoder is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
        public static Task<Image<TPixel>> LoadAsync<TPixel>(string path, IImageDecoder decoder)
            where TPixel : unmanaged, IPixel<TPixel>
            => LoadAsync<TPixel>(Configuration.Default, path, decoder, default);

        /// <summary>
        /// Create a new instance of the <see cref="Image"/> class from the given file.
        /// </summary>
        /// <param name="configuration">The Configuration.</param>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="ArgumentNullException">The decoder is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
        public static Task<Image> LoadAsync(
            Configuration configuration,
            string path,
            IImageDecoder decoder,
            CancellationToken cancellationToken = default)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(path, nameof(path));

            using Stream stream = configuration.FileSystem.OpenRead(path);
            return LoadAsync(configuration, stream, decoder, cancellationToken);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image"/> class from the given file.
        /// </summary>
        /// <param name="configuration">The Configuration.</param>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="ArgumentNullException">The decoder is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
        public static Task<Image<TPixel>> LoadAsync<TPixel>(
            Configuration configuration,
            string path,
            IImageDecoder decoder,
            CancellationToken cancellationToken = default)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(path, nameof(path));

            using Stream stream = configuration.FileSystem.OpenRead(path);
            return LoadAsync<TPixel>(configuration, stream, decoder, cancellationToken);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image"/> class from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
        public static Task<Image<TPixel>> LoadAsync<TPixel>(string path)
            where TPixel : unmanaged, IPixel<TPixel>
            => LoadAsync<TPixel>(Configuration.Default, path, default(CancellationToken));

        /// <summary>
        /// Create a new instance of the <see cref="Image"/> class from the given file.
        /// </summary>
        /// <param name="configuration">The configuration for the decoder.</param>
        /// <param name="path">The file path to the image.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
        public static async Task<Image<TPixel>> LoadAsync<TPixel>(
            Configuration configuration,
            string path,
            CancellationToken cancellationToken = default)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Stream stream = configuration.FileSystem.OpenRead(path);
            (Image<TPixel> img, _) = await LoadWithFormatAsync<TPixel>(configuration, stream, cancellationToken)
                .ConfigureAwait(false);
            return img;
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image"/> class from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="ArgumentNullException">The decoder is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(string path, IImageDecoder decoder)
            => Load(Configuration.Default, path, decoder);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(string path)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, path);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(string path, out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, path, out format);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="path">The file path to the image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration configuration, string path)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(path, nameof(path));

            using (Stream stream = configuration.FileSystem.OpenRead(path))
            {
                return Load<TPixel>(configuration, stream);
            }
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="path">The file path to the image.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration configuration, string path, out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(path, nameof(path));

            using (Stream stream = configuration.FileSystem.OpenRead(path))
            {
                return Load<TPixel>(configuration, stream, out format);
            }
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image"/> class from the given file.
        /// The pixel type is selected by the decoder.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="path">The file path to the image.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image Load(Configuration configuration, string path, out IImageFormat format)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(path, nameof(path));

            using (Stream stream = configuration.FileSystem.OpenRead(path))
            {
                return Load(configuration, stream, out format);
            }
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(string path, IImageDecoder decoder)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, path, decoder);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
        /// </summary>
        /// <param name="configuration">The Configuration.</param>
        /// <param name="path">The file path to the image.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="ArgumentNullException">The decoder is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration configuration, string path, IImageDecoder decoder)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(path, nameof(path));

            using (Stream stream = configuration.FileSystem.OpenRead(path))
            {
                return Load<TPixel>(configuration, stream, decoder);
            }
        }
    }
}
