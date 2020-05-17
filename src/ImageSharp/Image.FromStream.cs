// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
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
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <returns>The format type or null if none found.</returns>
        public static Task<IImageFormat> DetectFormatAsync(Stream stream)
            => DetectFormatAsync(Configuration.Default, stream);

        /// <summary>
        /// By reading the header on the provided stream this calculates the images format type.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="stream">The image stream to read the header from.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <returns>The format type or null if none found.</returns>
        public static Task<IImageFormat> DetectFormatAsync(Configuration configuration, Stream stream)
            => WithSeekableStreamAsync(configuration, stream, s => InternalDetectFormatAsync(s, configuration));

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="stream">The image stream to read the header from.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if suitable info detector not found.
        /// </returns>
        public static IImageInfo Identify(Stream stream)
            => Identify(stream, out IImageFormat _);

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="stream">The image stream to read the header from.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if suitable info detector not found.
        /// </returns>
        public static Task<IImageInfo> IdentifyAsync(Stream stream)
            => IdentifyAsync(Configuration.Default, stream);

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="stream">The image stream to read the header from.</param>
        /// <param name="format">The format type of the decoded image.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if suitable info detector not found.
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
        /// The <see cref="IImageInfo"/> or null if suitable info detector is not found.
        /// </returns>
        public static IImageInfo Identify(Configuration configuration, Stream stream)
            => Identify(configuration, stream, out _);

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
        /// The <see cref="IImageInfo"/> or null if suitable info detector is not found.
        /// </returns>
        public static async Task<IImageInfo> IdentifyAsync(Configuration configuration, Stream stream)
        {
            FormattedImageInfo res = await IdentifyWithFormatAsync(configuration, stream).ConfigureAwait(false);
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
        /// The <see cref="IImageInfo"/> or null if suitable info detector is not found.
        /// </returns>
        public static IImageInfo Identify(Configuration configuration, Stream stream, out IImageFormat format)
        {
            FormattedImageInfo data = WithSeekableStream(configuration, stream, s => InternalIdentity(s, configuration ?? Configuration.Default));

            format = data.Format;
            return data.ImageInfo;
        }

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="stream">The image stream to read the information from.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>
        /// The <see cref="FormattedImageInfo"/> with <see cref="FormattedImageInfo.ImageInfo"/> set to null if suitable info detector is not found.
        /// </returns>
        public static Task<FormattedImageInfo> IdentifyWithFormatAsync(Stream stream)
            => IdentifyWithFormatAsync(Configuration.Default, stream);

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
        /// The <see cref="FormattedImageInfo"/> with <see cref="FormattedImageInfo.ImageInfo"/> set to null if suitable info detector is not found.
        /// </returns>
        public static Task<FormattedImageInfo> IdentifyWithFormatAsync(Configuration configuration, Stream stream)
            => WithSeekableStreamAsync(configuration, stream, s => InternalIdentityAsync(s, configuration ?? Configuration.Default));

        /// <summary>
        /// Decode a new instance of the <see cref="Image"/> class from the given stream.
        /// The pixel format is selected by the decoder.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="format">The format type of the decoded image.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
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
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A <see cref="Task{FormattedImage}"/> representing the asynchronous operation.</returns>
        public static Task<FormattedImage> LoadWithFormatAsync(Stream stream)
            => LoadWithFormatAsync(Configuration.Default, stream);

        /// <summary>
        /// Decode a new instance of the <see cref="Image"/> class from the given stream.
        /// The pixel format is selected by the decoder.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(Stream stream) => Load(Configuration.Default, stream);

        /// <summary>
        /// Decode a new instance of the <see cref="Image"/> class from the given stream.
        /// The pixel format is selected by the decoder.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Task<Image> LoadAsync(Stream stream) => LoadAsync(Configuration.Default, stream);

        /// <summary>
        /// Decode a new instance of the <see cref="Image"/> class from the given stream.
        /// The pixel format is selected by the decoder.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="ArgumentNullException">The decoder is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
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
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="ArgumentNullException">The decoder is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Task<Image> LoadAsync(Stream stream, IImageDecoder decoder)
            => LoadAsync(Configuration.Default, stream, decoder);

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
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A new <see cref="Image"/>.</returns>>
        public static Image Load(Configuration configuration, Stream stream, IImageDecoder decoder)
        {
            Guard.NotNull(decoder, nameof(decoder));
            return WithSeekableStream(configuration, stream, s => decoder.Decode(configuration, s));
        }

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
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A new <see cref="Image"/>.</returns>>
        public static Task<Image> LoadAsync(Configuration configuration, Stream stream, IImageDecoder decoder)
        {
            Guard.NotNull(decoder, nameof(decoder));
            return WithSeekableStreamAsync(configuration, stream, s => decoder.DecodeAsync(configuration, s));
        }

        /// <summary>
        /// Decode a new instance of the <see cref="Image"/> class from the given stream.
        /// </summary>
        /// <param name="configuration">The configuration for the decoder.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A new <see cref="Image"/>.</returns>>
        public static Image Load(Configuration configuration, Stream stream) => Load(configuration, stream, out _);

        /// <summary>
        /// Decode a new instance of the <see cref="Image"/> class from the given stream.
        /// </summary>
        /// <param name="configuration">The configuration for the decoder.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A new <see cref="Image"/>.</returns>>
        public static async Task<Image> LoadAsync(Configuration configuration, Stream stream)
        {
            var fmt = await LoadWithFormatAsync(configuration, stream).ConfigureAwait(false);
            return fmt.Image;
        }

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, stream);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Task<Image<TPixel>> LoadAsync<TPixel>(Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
            => LoadAsync<TPixel>(Configuration.Default, stream);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="format">The format type of the decoded image.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Stream stream, out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(Configuration.Default, stream, out format);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task<FormattedImage<TPixel>> LoadWithFormatAsync<TPixel>(Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
            => LoadWithFormatAsync<TPixel>(Configuration.Default, stream);

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Stream stream, IImageDecoder decoder)
            where TPixel : unmanaged, IPixel<TPixel>
            => WithSeekableStream(Configuration.Default, stream, s => decoder.Decode<TPixel>(Configuration.Default, s));

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Task<Image<TPixel>> LoadAsync<TPixel>(Stream stream, IImageDecoder decoder)
            where TPixel : unmanaged, IPixel<TPixel>
            => WithSeekableStreamAsync(Configuration.Default, stream, s => decoder.DecodeAsync<TPixel>(Configuration.Default, s));

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="configuration">The Configuration.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Image<TPixel> Load<TPixel>(Configuration configuration, Stream stream, IImageDecoder decoder)
            where TPixel : unmanaged, IPixel<TPixel>
            => WithSeekableStream(configuration, stream, s => decoder.Decode<TPixel>(configuration, s));

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="configuration">The Configuration.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <param name="decoder">The decoder.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
        public static Task<Image<TPixel>> LoadAsync<TPixel>(Configuration configuration, Stream stream, IImageDecoder decoder)
            where TPixel : unmanaged, IPixel<TPixel>
            => WithSeekableStreamAsync(configuration, stream, s => decoder.DecodeAsync<TPixel>(configuration, s));

        /// <summary>
        /// Create a new instance of the <see cref="Image{TPixel}"/> class from the given stream.
        /// </summary>
        /// <param name="configuration">The configuration options.</param>
        /// <param name="stream">The stream containing image information.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>>
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
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(Configuration configuration, Stream stream, out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            (Image<TPixel> img, IImageFormat format) data = WithSeekableStream(configuration, stream, s => Decode<TPixel>(s, configuration));

            format = data.format;

            if (data.img != null)
            {
                return data.img;
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
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static async Task<FormattedImage> LoadWithFormatAsync(Configuration configuration, Stream stream)
        {
            (Image img, IImageFormat format) data = await WithSeekableStreamAsync(configuration, stream, s => DecodeAsync(s, configuration));

            if (data.img != null)
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
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static async Task<FormattedImage<TPixel>> LoadWithFormatAsync<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            (Image<TPixel> img, IImageFormat format) data = await WithSeekableStreamAsync(configuration, stream, s => DecodeAsync<TPixel>(s, configuration));

            if (data.img != null)
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
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The stream is null.</exception>
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static async Task<Image<TPixel>> LoadAsync<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            (Image<TPixel> img, _) = await LoadWithFormatAsync<TPixel>(configuration, stream).ConfigureAwait(false);
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
        /// <exception cref="NotSupportedException">The stream is not readable.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image Load(Configuration configuration, Stream stream, out IImageFormat format)
        {
            (Image img, IImageFormat format) data = WithSeekableStream(configuration, stream, s => Decode(s, configuration));

            format = data.format;

            if (data.img != null)
            {
                return data.img;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Image cannot be loaded. Available decoders:");

            foreach (KeyValuePair<IImageFormat, IImageDecoder> val in configuration.ImageFormatsManager.ImageDecoders)
            {
                sb.AppendFormat(" - {0} : {1}{2}", val.Key.Name, val.Value.GetType().Name, Environment.NewLine);
            }

            throw new UnknownImageFormatException(sb.ToString());
        }

        private static T WithSeekableStream<T>(Configuration configuration, Stream stream, Func<Stream, T> action)
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
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                return action(memoryStream);
            }
        }

        private static async Task<T> WithSeekableStreamAsync<T>(Configuration configuration, Stream stream, Func<Stream, Task<T>> action)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.NotNull(stream, nameof(stream));

            if (!stream.CanRead)
            {
                throw new NotSupportedException("Cannot read from the stream.");
            }

            // to make sure we don't trigger anything with aspnetcore then we just need to make sure we are seekable and we make the copy using CopyToAsync
            // if the stream is seekable then we arn't using one of the aspnetcore wrapped streams that error on sync api calls and we can use it with out
            // having to further wrap
            if (stream.CanSeek)
            {
                if (configuration.ReadOrigin == ReadOrigin.Begin)
                {
                    stream.Position = 0;
                }

                return await action(stream).ConfigureAwait(false);
            }

            using (var memoryStream = new MemoryStream()) // should really find a nice way to use a pool for these!!
            {
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                return await action(memoryStream).ConfigureAwait(false);
            }
        }
    }
}
