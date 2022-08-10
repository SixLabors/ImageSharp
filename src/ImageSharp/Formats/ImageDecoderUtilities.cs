// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.IO;
using System.Threading;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats
{
    internal static class ImageDecoderUtilities
    {
        public static IImageInfo Identify(
            this IImageDecoderInternals decoder,
            Configuration configuration,
            Stream stream,
            CancellationToken cancellationToken)
        {
            using var bufferedReadStream = new BufferedReadStream(configuration, stream);

            try
            {
                return decoder.Identify(bufferedReadStream, cancellationToken);
            }
            catch (InvalidMemoryOperationException ex)
            {
                throw new InvalidImageContentException(decoder.Dimensions, ex);
            }
        }

        public static Image<TPixel> Decode<TPixel>(
            this IImageDecoderInternals decoder,
            Configuration configuration,
            Stream stream,
            CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
            => decoder.Decode<TPixel>(configuration, stream, DefaultLargeImageExceptionFactory, cancellationToken);

        public static Image<TPixel> Decode<TPixel>(
            this IImageDecoderInternals decoder,
            Configuration configuration,
            Stream stream,
            Func<InvalidMemoryOperationException, Size, InvalidImageContentException> largeImageExceptionFactory,
            CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using var bufferedReadStream = new BufferedReadStream(configuration, stream);

            try
            {
                return decoder.Decode<TPixel>(bufferedReadStream, cancellationToken);
            }
            catch (InvalidMemoryOperationException ex)
            {
                throw largeImageExceptionFactory(ex, decoder.Dimensions);
            }
        }

        private static InvalidImageContentException DefaultLargeImageExceptionFactory(
            InvalidMemoryOperationException memoryOperationException,
            Size dimensions) =>
            new(dimensions, memoryOperationException);
    }
}
