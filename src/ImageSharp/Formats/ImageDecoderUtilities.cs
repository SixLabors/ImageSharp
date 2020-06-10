// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats
{
    internal static class ImageDecoderUtilities
    {
        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="decoder">The decoder.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        public static async Task<IImageInfo> IdentifyAsync(this IImageDecoderInternals decoder, Stream stream)
        {
            if (stream.CanSeek)
            {
                return decoder.Identify(stream);
            }

            using MemoryStream ms = decoder.Configuration.MemoryAllocator.AllocateFixedCapacityMemoryStream(stream.Length);
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            ms.Position = 0;
            return decoder.Identify(ms);
        }

        /// <summary>
        /// Decodes the image from the specified stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="decoder">The decoder.</param>
        /// <param name="stream">The stream, where the image should be decoded from. Cannot be null.</param>
        /// <exception cref="System.ArgumentNullException">
        ///    <para><paramref name="stream"/> is null.</para>
        /// </exception>
        /// <returns>The decoded image.</returns>
        public static async Task<Image<TPixel>> DecodeAsync<TPixel>(this IImageDecoderInternals decoder, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (stream.CanSeek)
            {
                return decoder.Decode<TPixel>(stream);
            }

            using MemoryStream ms = decoder.Configuration.MemoryAllocator.AllocateFixedCapacityMemoryStream(stream.Length);
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            ms.Position = 0;
            return decoder.Decode<TPixel>(ms);
        }
    }
}
