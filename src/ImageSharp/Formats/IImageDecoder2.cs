// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// Encapsulates properties and methods required for decoding an image from a stream.
    /// </summary>
    public interface IImageDecoder2
    {
        /// <summary>
        /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="options">The general decoder options.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
        Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>;

        /// <summary>
        /// Decodes the image from the specified stream to an <see cref="Image"/>.
        /// </summary>
        /// <param name="options">The general decoder options.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
        Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken);
    }
}
