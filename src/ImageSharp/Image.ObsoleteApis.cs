// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <content>
    /// Old obsolete APIs
    /// </content>
    public abstract partial class Image
    {
        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="data">The byte array containing encoded image data to read the header from.</param>
        /// <param name="format">The format type of the decoded image.</param>
        /// <exception cref="ArgumentNullException">The data is null.</exception>
        /// <exception cref="NotSupportedException">The data is not readable.</exception>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if suitable info detector not found.
        /// </returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IImageInfo Identify(byte[] data, out IImageFormat format) => Identify(Configuration.Default, data, out format);

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="data">The byte array containing encoded image data to read the header from.</param>
        /// <param name="format">The format type of the decoded image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The data is null.</exception>
        /// <exception cref="NotSupportedException">The data is not readable.</exception>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if suitable info detector is not found.
        /// </returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IImageInfo Identify(Configuration configuration, byte[] data, out IImageFormat format)
        {
            IImageInfo info = Identify(configuration, data);
            format = info.Metadata?.OrigionalImageFormat;
            return info;
        }

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <exception cref="ArgumentNullException">The data is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="NotSupportedException">Image format is not supported.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Image<TPixel> Load<TPixel>(byte[] data, out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, data, out format);

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte array.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="data">The byte array containing encoded image data.</param>
        /// <param name="format">The <see cref="IImageFormat"/> of the decoded image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The data is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="NotSupportedException">Image format is not supported.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Image<TPixel> Load<TPixel>(Configuration configuration, byte[] data, out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(data, nameof(data));

            using (var stream = new MemoryStream(data, 0, data.Length, false, true))
            {
                return Load<TPixel>(configuration, stream, out format);
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte span.
        /// </summary>
        /// <param name="data">The byte span containing image data.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <exception cref="NotSupportedException">Image format is not supported.</exception>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Image<TPixel> Load<TPixel>(ReadOnlySpan<byte> data, out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, data, out format);

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte span.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="data">The byte span containing image data.</param>
        /// <param name="format">The <see cref="IImageFormat"/> of the decoded image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <exception cref="NotSupportedException">Image format is not supported.</exception>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static unsafe Image<TPixel> Load<TPixel>(
            Configuration configuration,
            ReadOnlySpan<byte> data,
            out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            fixed (byte* ptr = &data.GetPinnableReference())
            {
                using (var stream = new UnmanagedMemoryStream(ptr, data.Length))
                {
                    return Load<TPixel>(configuration, stream, out format);
                }
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="format">The detected format.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The data is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <exception cref="NotSupportedException">Image format is not supported.</exception>
        /// <returns>The <see cref="Image"/>.</returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Image Load(byte[] data, out IImageFormat format)
            => Load(Configuration.Default, data, out format);

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte array.
        /// </summary>
        /// <param name="configuration">The configuration for the decoder.</param>
        /// <param name="data">The byte array containing image data.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The data is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <exception cref="NotSupportedException">Image format is not supported.</exception>
        /// <returns>The <see cref="Image"/>.</returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Image Load(Configuration configuration, byte[] data, out IImageFormat format)
        {
            var img = Load(configuration, data);
            format = img?.Metadata.OrigionalImageFormat;
            return img;
        }

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte array.
        /// </summary>
        /// <param name="data">The byte span containing image data.</param>
        /// <param name="format">The detected format.</param>
        /// <exception cref="ArgumentNullException">The decoder is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <exception cref="NotSupportedException">Image format is not supported.</exception>
        /// <returns>The <see cref="Image"/>.</returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Image Load(ReadOnlySpan<byte> data, out IImageFormat format)
            => Load(Configuration.Default, data, out format);

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte span.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="data">The byte span containing image data.</param>
        /// <param name="format">The <see cref="IImageFormat"/> of the decoded image.</param>>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <exception cref="NotSupportedException">Image format is not supported.</exception>
        /// <returns>The <see cref="Image"/>.</returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static unsafe Image Load(
            Configuration configuration,
            ReadOnlySpan<byte> data,
            out IImageFormat format)
        {
            var image = Load(configuration, data);
            format = image.Metadata.OrigionalImageFormat;
            return image;
        }

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="filePath">The image file to open and to read the header from.</param>
        /// <param name="format">The format type of the decoded image.</param>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if suitable info detector not found.
        /// </returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
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
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IImageInfo Identify(Configuration configuration, string filePath, out IImageFormat format)
        {
            IImageInfo info = Identify(configuration, filePath);
            format = info?.Metadata?.OrigionalImageFormat;
            return info;
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
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
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
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static async Task<(IImageInfo ImageInfo, IImageFormat Format)> IdentifyWithFormatAsync(
            Configuration configuration,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            IImageInfo info = await IdentifyAsync(configuration, filePath, cancellationToken)
                    .ConfigureAwait(false);
            return (info, info?.Metadata?.OrigionalImageFormat);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image"/> class from the given file.
        /// </summary>
        /// <param name="path">The file path to the image.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the stream is not readable nor seekable.
        /// </exception>
        /// <returns>A new <see cref="Image{Rgba32}"/>.</returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Image Load(string path, out IImageFormat format)
            => Load(Configuration.Default, path, out format);

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
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Image<TPixel> Load<TPixel>(string path, out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, path, out format);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given file.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="path">The file path to the image.</param>
        /// <param name="format">The mime type of the decoded image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="NotSupportedException">Image format is not supported.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
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
        /// <exception cref="NotSupportedException">Image format is not supported.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Image Load(Configuration configuration, string path, out IImageFormat format)
        {
            Image img = Load(configuration, path);
            format = img.Metadata.OrigionalImageFormat;
            return img;
        }

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
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IImageInfo Identify(Stream stream, out IImageFormat format)
            => Identify(Configuration.Default, stream, out format);

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="stream">The image stream to read the information from.</param>
        /// <param name="format">The format type of the decoded image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if a suitable info detector is not found.
        /// </returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IImageInfo Identify(Configuration configuration, Stream stream, out IImageFormat format)
        {
            IImageInfo imageInfo = Identify(configuration, stream);
            format = imageInfo?.Metadata?.OrigionalImageFormat;
            return imageInfo;
        }

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="stream">The image stream to read the information from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>
        /// The <see cref="Task{ValueTuple}"/> representing the asynchronous operation with the parameter type
        /// <see cref="IImageInfo"/> property set to null if suitable info detector is not found.
        /// </returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Task<(IImageInfo ImageInfo, IImageFormat Format)> IdentifyWithFormatAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
            => IdentifyWithFormatAsync(Configuration.Default, stream, cancellationToken);

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="stream">The image stream to read the information from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>
        /// The <see cref="Task{ValueTuple}"/> representing the asynchronous operation with the parameter type
        /// <see cref="IImageInfo"/> property set to null if suitable info detector is not found.
        /// </returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static async Task<(IImageInfo ImageInfo, IImageFormat Format)> IdentifyWithFormatAsync(
            Configuration configuration,
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            var info = await IdentifyAsync(configuration, stream, cancellationToken);

            return (info, info.Metadata?.OrigionalImageFormat);
        }

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
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Image Load(Stream stream, out IImageFormat format)
            => Load(Configuration.Default, stream, out format);

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
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Task<(Image Image, IImageFormat Format)> LoadWithFormatAsync(Stream stream, CancellationToken cancellationToken = default)
            => LoadWithFormatAsync(Configuration.Default, stream, cancellationToken);

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
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Image<TPixel> Load<TPixel>(Stream stream, out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, stream, out format);

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
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Task<(Image<TPixel> Image, IImageFormat Format)> LoadWithFormatAsync<TPixel>(Stream stream, CancellationToken cancellationToken = default)
            where TPixel : unmanaged, IPixel<TPixel>
            => LoadWithFormatAsync<TPixel>(Configuration.Default, stream, cancellationToken);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="format">The format type of the decoded image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Image<TPixel> Load<TPixel>(Configuration configuration, Stream stream, out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Image<TPixel> image = WithSeekableStream(configuration, stream, s => Decode<TPixel>(s, configuration));
            format = image.Metadata.OrigionalImageFormat;
            return image;
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image"/> class from the given stream.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A <see cref="Task{ValueTuple}"/> representing the asynchronous operation.</returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static async Task<(Image Image, IImageFormat Format)> LoadWithFormatAsync(
            Configuration configuration,
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            Image image = await LoadAsync(configuration, stream, cancellationToken);
            return (image, image.Metadata?.OrigionalImageFormat);
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A <see cref="Task{ValueTuple}"/> representing the asynchronous operation.</returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static async Task<(Image<TPixel> Image, IImageFormat Format)> LoadWithFormatAsync<TPixel>(
            Configuration configuration,
            Stream stream,
            CancellationToken cancellationToken = default)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Image<TPixel> image = await LoadAsync<TPixel>(configuration, stream, cancellationToken);
            return (image, image.Metadata?.OrigionalImageFormat);
        }

        /// <summary>
        /// Decode a new instance of the <see cref="Image"/> class from the given stream.
        /// The pixel format is selected by the decoder.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="format">The format type of the decoded image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        [Obsolete("Format accessable from Metadata.OriginalImageFormat")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Image Load(Configuration configuration, Stream stream, out IImageFormat format)
        {
            var img = Load(configuration, stream);
            format = img.Metadata?.OrigionalImageFormat;
            return img;
        }
    }
}
