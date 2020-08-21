// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// Abstraction for shared internals for ***DecoderCore implementations to be used with <see cref="ImageDecoderUtilities"/>.
    /// </summary>
    internal interface IImageDecoderInternals
    {
        /// <summary>
        /// Gets the associated configuration.
        /// </summary>
        Configuration Configuration { get; }

        /// <summary>
        /// Gets the dimensions of the image being decoded.
        /// </summary>
        Size Dimensions { get; }

        /// <summary>
        /// Decodes the image from the specified stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="stream">The stream, where the image should be decoded from. Cannot be null.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        /// <returns>The decoded image.</returns>
        /// <remarks>
        /// Cancellable synchronous method. In case of cancellation,
        /// an <see cref="OperationCanceledException"/> shall be thrown which will be handled on the call site.
        /// </remarks>
        Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>;

        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The <see cref="IImageInfo"/>.</returns>
        /// <remarks>
        /// Cancellable synchronous method. In case of cancellation,
        /// an <see cref="OperationCanceledException"/> shall be thrown which will be handled on the call site.
        /// </remarks>
        IImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken);
    }
}
