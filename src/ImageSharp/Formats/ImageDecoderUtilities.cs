// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats
{
    internal static class ImageDecoderUtilities
    {
        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="decoder">The decoder.</param>
        /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task<IImageInfo> IdentifyAsync(this IImageDecoderInternals decoder, BufferedReadStream stream)
            => Task.FromResult(decoder.Identify(stream));

        /// <summary>
        /// Decodes the image from the specified stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="decoder">The decoder.</param>
        /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task<Image<TPixel>> DecodeAsync<TPixel>(this IImageDecoderInternals decoder, BufferedReadStream stream)
            where TPixel : unmanaged, IPixel<TPixel>
            => Task.FromResult(decoder.Decode<TPixel>(stream));
    }
}
