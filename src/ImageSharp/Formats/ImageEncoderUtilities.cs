// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats
{
    internal static class ImageEncoderUtilities
    {
        public static async Task EncodeAsync<TPixel>(
            this IImageEncoderInternals encoder,
            Image<TPixel> image,
            Stream stream,
            CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Configuration configuration = image.GetConfiguration();
            if (stream.CanSeek)
            {
                encoder.Encode(image, stream, cancellationToken);
            }
            else
            {
                using var ms = new MemoryStream();
                encoder.Encode(image, ms, cancellationToken);
                ms.Position = 0;
                await ms.CopyToAsync(stream, configuration.StreamProcessingBufferSize, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public static void Encode<TPixel>(
            this IImageEncoderInternals encoder,
            Image<TPixel> image,
            Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
            => encoder.Encode(image, stream, default);
    }
}
