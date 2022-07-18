// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <content>
    /// Adds static methods allowing the creation of new image from a byte span.
    /// </content>
    public abstract partial class Image
    {
        /// <summary>
        /// By reading the header on the provided byte span this calculates the images format.
        /// </summary>
        /// <param name="data">The byte span containing encoded image data to read the header from.</param>
        /// <returns>The format or null if none found.</returns>
        public static IImageFormat DetectFormat(ReadOnlySpan<byte> data)
            => DetectFormat(DecoderOptions.Default, data);

        /// <summary>
        /// By reading the header on the provided byte span this calculates the images format.
        /// </summary>
        /// <param name="options">The general decoder options.</param>
        /// <param name="data">The byte span containing encoded image data to read the header from.</param>
        /// <exception cref="ArgumentNullException">The options are null.</exception>
        /// <returns>The mime type or null if none found.</returns>
        public static IImageFormat DetectFormat(DecoderOptions options, ReadOnlySpan<byte> data)
        {
            Guard.NotNull(options, nameof(options.Configuration));

            Configuration configuration = options.Configuration;
            int maxHeaderSize = configuration.MaxHeaderSize;
            if (maxHeaderSize <= 0)
            {
                return null;
            }

            foreach (IImageFormatDetector detector in configuration.ImageFormatsManager.FormatDetectors)
            {
                IImageFormat f = detector.DetectFormat(data);

                if (f != null)
                {
                    return f;
                }
            }

            return default;
        }

        /// <summary>
        /// Reads the raw image information from the specified stream without fully decoding it.
        /// </summary>
        /// <param name="data">The byte span containing encoded image data to read the header from.</param>
        /// <exception cref="ArgumentNullException">The data is null.</exception>
        /// <exception cref="NotSupportedException">The data is not readable.</exception>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if suitable info detector not found.
        /// </returns>
        public static IImageInfo Identify(ReadOnlySpan<byte> data) => Identify(data, out IImageFormat _);

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
        public static IImageInfo Identify(ReadOnlySpan<byte> data, out IImageFormat format)
            => Identify(DecoderOptions.Default, data, out format);

        /// <summary>
        /// Reads the raw image information from the specified span of bytes without fully decoding it.
        /// </summary>
        /// <param name="options">The general decoder options.</param>
        /// <param name="data">The byte span containing encoded image data to read the header from.</param>
        /// <param name="format">The format type of the decoded image.</param>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentNullException">The data is null.</exception>
        /// <exception cref="NotSupportedException">The data is not readable.</exception>
        /// <returns>
        /// The <see cref="IImageInfo"/> or null if suitable info detector is not found.
        /// </returns>
        public static unsafe IImageInfo Identify(DecoderOptions options, ReadOnlySpan<byte> data, out IImageFormat format)
        {
            fixed (byte* ptr = data)
            {
                using var stream = new UnmanagedMemoryStream(ptr, data.Length);
                return Identify(options, stream, out format);
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte span.
        /// </summary>
        /// <param name="data">The byte span containing encoded image data.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <exception cref="NotSupportedException">Image format is not supported.</exception>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Load<TPixel>(ReadOnlySpan<byte> data)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(DecoderOptions.Default, data);

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
        public static Image<TPixel> Load<TPixel>(ReadOnlySpan<byte> data, out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
            => Load<TPixel>(DecoderOptions.Default, data, out format);

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte span.
        /// </summary>
        /// <param name="options">The general decoder options.</param>
        /// <param name="data">The byte span containing encoded image data.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <exception cref="ArgumentNullException">The options are null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <exception cref="NotSupportedException">Image format is not supported.</exception>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static unsafe Image<TPixel> Load<TPixel>(DecoderOptions options, ReadOnlySpan<byte> data)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            fixed (byte* ptr = data)
            {
                using var stream = new UnmanagedMemoryStream(ptr, data.Length);
                return Load<TPixel>(options, stream);
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image{TPixel}"/> from the given encoded byte span.
        /// </summary>
        /// <param name="options">The general decoder options.</param>
        /// <param name="data">The byte span containing image data.</param>
        /// <param name="format">The <see cref="IImageFormat"/> of the decoded image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <exception cref="ArgumentNullException">The options are null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <exception cref="NotSupportedException">Image format is not supported.</exception>
        /// <returns>A new <see cref="Image{TPixel}"/>.</returns>
        public static unsafe Image<TPixel> Load<TPixel>(
            DecoderOptions options,
            ReadOnlySpan<byte> data,
            out IImageFormat format)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            fixed (byte* ptr = data)
            {
                using var stream = new UnmanagedMemoryStream(ptr, data.Length);
                return Load<TPixel>(options, stream, out format);
            }
        }

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte span.
        /// </summary>
        /// <param name="data">The byte span containing image data.</param>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <exception cref="NotSupportedException">Image format is not supported.</exception>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(ReadOnlySpan<byte> data)
            => Load(DecoderOptions.Default, data);

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
        public static Image Load(ReadOnlySpan<byte> data, out IImageFormat format)
            => Load(DecoderOptions.Default, data, out format);

        /// <summary>
        /// Decodes a new instance of <see cref="Image"/> from the given encoded byte span.
        /// </summary>
        /// <param name="options">The general decoder options.</param>
        /// <param name="data">The byte span containing image data.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Load(DecoderOptions options, ReadOnlySpan<byte> data)
            => Load(options, data, out _);

        /// <summary>
        /// Load a new instance of <see cref="Image"/> from the given encoded byte span.
        /// </summary>
        /// <param name="options">The general decoder options.</param>
        /// <param name="data">The byte span containing image data.</param>
        /// <param name="format">The <see cref="IImageFormat"/> of the decoded image.</param>>
        /// <exception cref="ArgumentNullException">The options are null.</exception>
        /// <exception cref="UnknownImageFormatException">Image format not recognised.</exception>
        /// <exception cref="InvalidImageContentException">Image contains invalid content.</exception>
        /// <exception cref="NotSupportedException">Image format is not supported.</exception>
        /// <returns>The <see cref="Image"/>.</returns>
        public static unsafe Image Load(
            DecoderOptions options,
            ReadOnlySpan<byte> data,
            out IImageFormat format)
        {
            fixed (byte* ptr = data)
            {
                using var stream = new UnmanagedMemoryStream(ptr, data.Length);
                return Load(options, stream, out format);
            }
        }
    }
}
