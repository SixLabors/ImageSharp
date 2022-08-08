// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.IO;
using System.Threading;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// Utility methods for <see cref="IImageDecoder"/>.
    /// </summary>
    internal static class ImageDecoderUtilities
    {
        /// <summary>
        /// Performs a resize operation against the decoded image. If the target size is not set, or the image size
        /// already matches the target size, the image is untouched.
        /// </summary>
        /// <param name="options">The decoder options.</param>
        /// <param name="image">The decoded image.</param>
        public static void Resize(DecoderOptions options, Image image)
        {
            if (ShouldResize(options, image))
            {
                ResizeOptions resizeOptions = new()
                {
                    Size = options.TargetSize.Value,
                    Sampler = KnownResamplers.Box,
                    Mode = ResizeMode.Max
                };

                image.Mutate(x => x.Resize(resizeOptions));
            }
        }

        /// <summary>
        /// Determines whether the decoded image should be resized.
        /// </summary>
        /// <param name="options">The decoder options.</param>
        /// <param name="image">The decoded image.</param>
        /// <returns><see langword="true"/> if the image should be resized, otherwise; <see langword="false"/>.</returns>
        private static bool ShouldResize(DecoderOptions options, Image image)
        {
            if (options.TargetSize is null)
            {
                return false;
            }

            Size targetSize = options.TargetSize.Value;
            Size currentSize = image.Size();
            return currentSize.Width != targetSize.Width && currentSize.Height != targetSize.Height;
        }

        internal static IImageInfo Identify(
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

        internal static Image<TPixel> Decode<TPixel>(
            this IImageDecoderInternals decoder,
            Configuration configuration,
            Stream stream,
            CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
            => decoder.Decode<TPixel>(configuration, stream, DefaultLargeImageExceptionFactory, cancellationToken);

        internal static Image<TPixel> Decode<TPixel>(
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
