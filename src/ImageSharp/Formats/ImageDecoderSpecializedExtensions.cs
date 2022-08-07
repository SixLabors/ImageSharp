// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// Extensions methods for <see cref="IImageDecoderSpecialized{T}"/>.
    /// </summary>
    public static class ImageDecoderSpecializedExtensions
    {
        /// <summary>
        /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
        /// </summary>
        /// <typeparam name="T">The type of specialized options.</typeparam>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="decoder">The decoder.</param>
        /// <param name="options">The specialized decoder options.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
        public static Image<TPixel> DecodeSpecialized<T, TPixel>(this IImageDecoderSpecialized<T> decoder, T options, Stream stream)
            where T : ISpecializedDecoderOptions
            where TPixel : unmanaged, IPixel<TPixel>
            => decoder.DecodeSpecialized<TPixel>(options, stream, default);

        /// <summary>
        /// Decodes the image from the specified stream to an <see cref="Image"/> of a specific pixel type.
        /// </summary>
        /// <typeparam name="T">The type of specialized options.</typeparam>
        /// <param name="decoder">The decoder.</param>
        /// <param name="options">The specialized decoder options.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
        public static Image DecodeSpecialized<T>(this IImageDecoderSpecialized<T> decoder, T options, Stream stream)
            where T : ISpecializedDecoderOptions
            => decoder.DecodeSpecialized(options, stream, default);

        /// <summary>
        /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
        /// </summary>
        /// <typeparam name="T">The type of specialized options.</typeparam>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="decoder">The decoder.</param>
        /// <param name="options">The specialized decoder options.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
        /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
        public static Task<Image<TPixel>> DecodeSpecializedAsync<T, TPixel>(this IImageDecoderSpecialized<T> decoder, T options, Stream stream, CancellationToken cancellationToken = default)
            where T : ISpecializedDecoderOptions
            where TPixel : unmanaged, IPixel<TPixel>
            => Image.WithSeekableStreamAsync(
                options.GeneralOptions,
                stream,
                (s, ct) => decoder.DecodeSpecialized<TPixel>(options, s, ct),
                cancellationToken);

        /// <summary>
        /// Decodes the image from the specified stream to an <see cref="Image"/> of a specific pixel type.
        /// </summary>
        /// <typeparam name="T">The type of specialized options.</typeparam>
        /// <param name="decoder">The decoder.</param>
        /// <param name="options">The specialized decoder options.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task{Image}"/> representing the asynchronous operation.</returns>
        /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
        public static Task<Image> DecodeSpecializedAsync<T>(this IImageDecoderSpecialized<T> decoder, T options, Stream stream, CancellationToken cancellationToken = default)
            where T : ISpecializedDecoderOptions
            => Image.WithSeekableStreamAsync(
                options.GeneralOptions,
                stream,
                (s, ct) => decoder.DecodeSpecialized(options, s, ct),
                cancellationToken);
    }
}
