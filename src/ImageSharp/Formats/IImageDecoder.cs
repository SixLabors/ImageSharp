// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// Encapsulates properties and methods required for decoding an image from a stream.
    /// </summary>
    public interface IImageDecoder
    {
        /// <summary>
        /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="configuration">The configuration for the image.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        // TODO: Document ImageFormatExceptions (https://github.com/SixLabors/ImageSharp/issues/1110)
        Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>;

        /// <summary>
        /// Decodes the image from the specified stream to an <see cref="Image"/>.
        /// </summary>
        /// <param name="configuration">The configuration for the image.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        // TODO: Document ImageFormatExceptions (https://github.com/SixLabors/ImageSharp/issues/1110)
        Image Decode(Configuration configuration, Stream stream);

        /// <summary>
        /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="configuration">The configuration for the image.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        // TODO: Document ImageFormatExceptions (https://github.com/SixLabors/ImageSharp/issues/1110)
        Task<Image<TPixel>> DecodeAsync<TPixel>(Configuration configuration, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>;

        /// <summary>
        /// Decodes the image from the specified stream to an <see cref="Image"/>.
        /// </summary>
        /// <param name="configuration">The configuration for the image.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        // TODO: Document ImageFormatExceptions (https://github.com/SixLabors/ImageSharp/issues/1110)
        Task<Image> DecodeAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken);
    }
}
