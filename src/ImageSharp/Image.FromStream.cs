// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <content>
    /// Adds static methods allowing the creation of new image from a given stream.
    /// </content>
    public abstract partial class Image
    {
        /// <summary>
        /// By reading the header on the provided stream this calculates the images format type.
        /// </summary>
        /// <param name="stream">The image stream to read the header from.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <returns>The format type or null if none found.</returns>
        public static IImageFormat DetectFormat(Stream stream)
            => DetectFormat(Configuration.Default, stream);

        /// <summary>
        /// By reading the header on the provided stream this calculates the images format type.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="stream">The image stream to read the header from.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <returns>The format type or null if none found.</returns>
        public static IImageFormat DetectFormat(Configuration configuration, Stream stream)
            => WithSeekableStream(configuration, stream, s => InternalDetectFormat(s, configuration));

        /// <summary>
        /// By reading the header on the provided stream this calculates the images format type.
        /// </summary>
        /// <param name="stream">The image stream to read the header from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <returns>A <see cref="Task{IImageFormat}"/> representing the asynchronous operation or null if none is found.</returns>
        public static Task<IImageFormat> DetectFormatAsync(Stream stream, CancellationToken cancellationToken = default)
            => DetectFormatAsync(Configuration.Default, stream, cancellationToken);

        /// <summary>
        /// By reading the header on the provided stream this calculates the images format type.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="stream">The image stream to read the header from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <returns>A <see cref="Task{IImageFormat}"/> representing the asynchronous operation.</returns>
        public static Task<IImageFormat> DetectFormatAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken = default)
            => WithSeekableStreamAsync(
                configuration,
                stream,
                (s, _) => InternalDetectFormat(s, configuration),
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
            => IdentifyAsync(Configuration.Default, stream, cancellationToken);

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
            => Identify(Configuration.Default, stream, out format);

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="stream">The image stream to read the information from.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if a suitable info detector is not found.
        /// </returns>
        public static IImageInfo Identify(Configuration configuration, Stream stream)
            => Identify(configuration, stream, out _);

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
        /// A <see cref="Task{IImageInfo}"/> representing the asynchronous operation or null if
        /// a suitable detector is not found.
        /// </returns>
        public static async Task<IImageInfo> IdentifyAsync(
            Configuration configuration,
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            (IImageInfo ImageInfo, IImageFormat Format) res = await IdentifyWithFormatAsync(configuration, stream, cancellationToken).ConfigureAwait(false);
            return res.ImageInfo;
        }

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
        public static IImageInfo Identify(Configuration configuration, Stream stream, out IImageFormat format)
        {
            (IImageInfo ImageInfo, IImageFormat Format) data = WithSeekableStream(configuration, stream, s => InternalIdentity(s, configuration ?? Configuration.Default));

            format = data.Format;
            return data.ImageInfo;
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
        public static Task<(IImageInfo ImageInfo, IImageFormat Format)> IdentifyWithFormatAsync(
            Configuration configuration,
            Stream stream,
            CancellationToken cancellationToken = default)
            => WithSeekableStreamAsync(
                configuration,
                stream,
                (s, ct) => InternalIdentity(s, configuration ?? Configuration.Default, ct),
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
        public static Task<(Image Image, IImageFormat Format)> LoadWithFormatAsync(Stream stream, CancellationToken cancellationToken = default)
            => LoadWithFormatAsync(Configuration.Default, stream, cancellationToken);

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
        public static Image Load(Stream stream) => Load(Configuration.Default, stream);

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
            => LoadAsync(Configuration.Default, stream, cancellationToken);

        /// <summary>
        /// Decode a new instance of the <see cref="Image"/> class from the given stream.
        /// The pixel format is selected by the decoder.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="ArgumentNullException">The decoder is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(Stream stream, IImageDecoder decoder)
            => Load(Configuration.Default, stream, decoder);

        /// <summary>
        /// Decode a new instance of the <see cref="Image"/> class from the given stream.
        /// The pixel format is selected by the decoder.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="ArgumentNullException">The decoder is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
        public static Task<Image> LoadAsync(Stream stream, IImageDecoder decoder, CancellationToken cancellationToken = default)
            => LoadAsync(Configuration.Default, stream, decoder, cancellationToken);

        /// <summary>
        /// Decode a new instance of the <see cref="Image"/> class from the given stream.
        /// The pixel format is selected by the decoder.
        /// </summary>
        /// <param name="configuration">The configuration for the decoder.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="ArgumentNullException">The decoder is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A new <see cref="Image"/>.</returns>
        public static Image Load(Configuration configuration, Stream stream, IImageDecoder decoder)
        {
            Guard.NotNull(decoder, nameof(decoder));
            return WithSeekableStream(configuration, stream, s => decoder.Decode(configuration, s, default));
        }

        /// <summary>
        /// Decode a new instance of the <see cref="Image"/> class from the given stream.
        /// The pixel format is selected by the decoder.
        /// </summary>
        /// <param name="configuration">The configuration for the decoder.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="ArgumentNullException">The decoder is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
        public static Task<Image> LoadAsync(
            Configuration configuration,
            Stream stream,
            IImageDecoder decoder,
            CancellationToken cancellationToken = default)
        {
            Guard.NotNull(decoder, nameof(decoder));
            return WithSeekableStreamAsync(
                configuration,
                stream,
                (s, ct) => decoder.Decode(configuration, s, ct),
                cancellationToken);
        }

        /// <summary>
        /// Decode a new instance of the <see cref="Image"/> class from the given stream.
        /// </summary>
        /// <param name="configuration">The configuration for the decoder.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A new <see cref="Image"/>.</returns>
        public static Image Load(Configuration configuration, Stream stream) => Load(configuration, stream, out _);

        /// <summary>
        /// Decode a new instance of the <see cref="Image"/> class from the given stream.
        /// </summary>
        /// <param name="configuration">The configuration for the decoder.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
        public static async Task<Image> LoadAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken = default)
        {
            (Image Image, IImageFormat Format) fmt = await LoadWithFormatAsync(configuration, stream, cancellationToken)
                .ConfigureAwait(false);
            return fmt.Image;
        }

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
            => Load<TPixel>(Configuration.Default, stream);

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
            => LoadAsync<TPixel>(Configuration.Default, stream, cancellationToken);

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
        public static Task<(Image<TPixel> Image, IImageFormat Format)> LoadWithFormatAsync<TPixel>(Stream stream, CancellationToken cancellationToken = default)
            where TPixel : unmanaged, IPixel<TPixel>
            => LoadWithFormatAsync<TPixel>(Configuration.Default, stream, cancellationToken);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Stream stream, IImageDecoder decoder)
            where TPixel : unmanaged, IPixel<TPixel>
            => WithSeekableStream(Configuration.Default, stream, s => decoder.Decode<TPixel>(Configuration.Default, s, default));

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task<Image<TPixel>> LoadAsync<TPixel>(Stream stream, IImageDecoder decoder, CancellationToken cancellationToken = default)
            where TPixel : unmanaged, IPixel<TPixel>
            => WithSeekableStreamAsync(
                Configuration.Default,
                stream,
                (s, ct) => decoder.Decode<TPixel>(Configuration.Default, s, ct),
                cancellationToken);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="configuration">The Configuration.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration configuration, Stream stream, IImageDecoder decoder)
            where TPixel : unmanaged, IPixel<TPixel>
            => WithSeekableStream(configuration, stream, s => decoder.Decode<TPixel>(configuration, s, default));

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="configuration">The Configuration.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task<Image<TPixel>> LoadAsync<TPixel>(
            Configuration configuration,
            Stream stream,
            IImageDecoder decoder,
            CancellationToken cancellationToken = default)
            where TPixel : unmanaged, IPixel<TPixel>
            => WithSeekableStreamAsync(
                configuration,
                stream,
                (s, ct) => decoder.Decode<TPixel>(configuration, s, ct),
                cancellationToken);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable or the image format is not supported.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(configuration, stream, out IImageFormat _);

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
        public static Image<TPixel> Load<TPixel>(Configuration configuration, Stream stream, out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            (Image<TPixel> Image, IImageFormat Format) data = WithSeekableStream(configuration, stream, s => Decode<TPixel>(s, configuration));

            format = data.Format;

            if (data.Image != null)
            {
                return data.Image;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Image cannot be loaded. Available decoders:");

            foreach (KeyValuePair<IImageFormat, IImageDecoder> val in configuration.ImageFormatsManager.ImageDecoders)
            {
                sb.AppendFormat(" - {0} : {1}{2}", val.Key.Name, val.Value.GetType().Name, Environment.NewLine);
            }

            throw new UnknownImageFormatException(sb.ToString());
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
        public static async Task<(Image Image, IImageFormat Format)> LoadWithFormatAsync(
            Configuration configuration,
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            (Image Image, IImageFormat Format) data = await WithSeekableStreamAsync(
                    configuration,
                    stream,
                    (s, ct) => Decode(s, configuration, ct),
                    cancellationToken)
                .ConfigureAwait(false);

            if (data.Image != null)
            {
                return data;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Image cannot be loaded. Available decoders:");

            foreach (KeyValuePair<IImageFormat, IImageDecoder> val in configuration.ImageFormatsManager.ImageDecoders)
            {
                sb.AppendFormat(" - {0} : {1}{2}", val.Key.Name, val.Value.GetType().Name, Environment.NewLine);
            }

            throw new UnknownImageFormatException(sb.ToString());
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
        public static async Task<(Image<TPixel> Image, IImageFormat Format)> LoadWithFormatAsync<TPixel>(
            Configuration configuration,
            Stream stream,
            CancellationToken cancellationToken = default)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            (Image<TPixel> Image, IImageFormat Format) data =
                await WithSeekableStreamAsync(
                    configuration,
                    stream,
                    (s, ct) => Decode<TPixel>(s, configuration, ct),
                    cancellationToken)
                .ConfigureAwait(false);

            if (data.Image != null)
            {
                return data;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Image cannot be loaded. Available decoders:");

            foreach (KeyValuePair<IImageFormat, IImageDecoder> val in configuration.ImageFormatsManager.ImageDecoders)
            {
                sb.AppendFormat(" - {0} : {1}{2}", val.Key.Name, val.Value.GetType().Name, Environment.NewLine);
            }

            throw new UnknownImageFormatException(sb.ToString());
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
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task<Image<TPixel>> LoadAsync<TPixel>(
            Configuration configuration,
            Stream stream,
            CancellationToken cancellationToken = default)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            (Image<TPixel> img, _) = await LoadWithFormatAsync<TPixel>(configuration, stream, cancellationToken)
                .ConfigureAwait(false);
            return img;
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
        public static Image Load(Configuration configuration, Stream stream, out IImageFormat format)
        {
            (Image Img, IImageFormat Format) data = WithSeekableStream(configuration, stream, s => Decode(s, configuration));

            format = data.Format;

            if (data.Img != null)
            {
                return data.Img;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Image cannot be loaded. Available decoders:");

            foreach (KeyValuePair<IImageFormat, IImageDecoder> val in configuration.ImageFormatsManager.ImageDecoders)
            {
                sb.AppendFormat(" - {0} : {1}{2}", val.Key.Name, val.Value.GetType().Name, Environment.NewLine);
            }

            throw new UnknownImageFormatException(sb.ToString());
        }

        /// <summary>
        /// Performs the given action against the stream ensuring that it is seekable.
        /// </summary>
        /// <typeparam name="T">The type of object returned from the action.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="stream">The input stream.</param>
        /// <param name="action">The action to perform.</param>
        /// <returns>The <typeparamref name="T"/>.</returns>
        private static T WithSeekableStream<T>(
            Configuration configuration,
            Stream stream,
            Func<Stream, T> action)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(stream, nameof(stream));

            if (!stream.CanRead)
            {
                throw new NotSupportedException("Cannot read from the stream.");
            }

            if (stream.CanSeek)
            {
                if (configuration.ReadOrigin == ReadOrigin.Begin)
                {
                    stream.Position = 0;
                }

                return action(stream);
            }

            // We want to be able to load images from things like HttpContext.Request.Body
            using var memoryStream = new ChunkedMemoryStream(configuration.MemoryAllocator);
            stream.CopyTo(memoryStream, configuration.StreamProcessingBufferSize);
            memoryStream.Position = 0;

            return action(memoryStream);
        }

        /// <summary>
        /// Performs the given action asynchronously against the stream ensuring that it is seekable.
        /// </summary>
        /// <typeparam name="T">The type of object returned from the action.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="stream">The input stream.</param>
        /// <param name="action">The action to perform.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The <see cref="Task{T}"/>.</returns>
        private static async Task<T> WithSeekableStreamAsync<T>(
            Configuration configuration,
            Stream stream,
            Func<Stream, CancellationToken, T> action,
            CancellationToken cancellationToken)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(stream, nameof(stream));

            if (!stream.CanRead)
            {
                throw new NotSupportedException("Cannot read from the stream.");
            }

            if (stream.CanSeek)
            {
                if (configuration.ReadOrigin == ReadOrigin.Begin)
                {
                    stream.Position = 0;
                }

                // NOTE: We are explicitly not executing the action against the stream here as we do in WithSeekableStream() because that
                // would incur synchronous IO reads which must be avoided in this asynchronous method.  Instead, we will *always* run the
                // code below to copy the stream to an in-memory buffer before invoking the action.
            }

            using var memoryStream = new ChunkedMemoryStream(configuration.MemoryAllocator);
            await stream.CopyToAsync(memoryStream, configuration.StreamProcessingBufferSize, cancellationToken).ConfigureAwait(false);
            memoryStream.Position = 0;

            return action(memoryStream, cancellationToken);
        }
    }
}
