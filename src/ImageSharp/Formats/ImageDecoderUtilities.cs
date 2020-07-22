// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.IO;
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
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task<IImageInfo> IdentifyAsync(
            this IImageDecoderInternals decoder,
            Stream stream,
            CancellationToken cancellationToken)
            => decoder.IdentifyAsync(stream, DefaultLargeImageExceptionFactory, cancellationToken);

        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="decoder">The decoder.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="tooLargeImageExceptionFactory">Factory method to handle <see cref="InvalidMemoryOperationException"/> as <see cref="InvalidImageContentException"/>.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task<IImageInfo> IdentifyAsync(
            this IImageDecoderInternals decoder,
            Stream stream,
            Func<InvalidMemoryOperationException, Size, InvalidImageContentException> tooLargeImageExceptionFactory,
            CancellationToken cancellationToken)
        {
            try
            {
                using BufferedReadStream bufferedReadStream = new BufferedReadStream(stream);
                IImageInfo imageInfo = decoder.Identify(bufferedReadStream, cancellationToken);
                return Task.FromResult(imageInfo);
            }
            catch (InvalidMemoryOperationException ex)
            {
                InvalidImageContentException invalidImageContentException = tooLargeImageExceptionFactory(ex, decoder.Dimensions);
                return Task.FromException<IImageInfo>(invalidImageContentException);
            }
            catch (OperationCanceledException)
            {
                return Task.FromCanceled<IImageInfo>(cancellationToken);
            }
            catch (Exception ex)
            {
                return Task.FromException<IImageInfo>(ex);
            }
        }

        /// <summary>
        /// Decodes the image from the specified stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="decoder">The decoder.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task<Image<TPixel>> DecodeAsync<TPixel>(
            this IImageDecoderInternals decoder,
            Stream stream,
            CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel> =>
            decoder.DecodeAsync<TPixel>(
                stream,
                DefaultLargeImageExceptionFactory,
                cancellationToken);

        /// <summary>
        /// Decodes the image from the specified stream.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="decoder">The decoder.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="largeImageExceptionFactory">Factory method to handle <see cref="InvalidMemoryOperationException"/> as <see cref="InvalidImageContentException"/>.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static Task<Image<TPixel>> DecodeAsync<TPixel>(
            this IImageDecoderInternals decoder,
            Stream stream,
            Func<InvalidMemoryOperationException, Size, InvalidImageContentException> largeImageExceptionFactory,
            CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            try
            {
                using BufferedReadStream bufferedReadStream = new BufferedReadStream(stream);
                Image<TPixel> image = decoder.Decode<TPixel>(bufferedReadStream, cancellationToken);
                return Task.FromResult(image);
            }
            catch (InvalidMemoryOperationException ex)
            {
                InvalidImageContentException invalidImageContentException = largeImageExceptionFactory(ex, decoder.Dimensions);
                return Task.FromException<Image<TPixel>>(invalidImageContentException);
            }
            catch (OperationCanceledException)
            {
                return Task.FromCanceled<Image<TPixel>>(cancellationToken);
            }
            catch (Exception ex)
            {
                return Task.FromException<Image<TPixel>>(ex);
            }
        }

        public static IImageInfo Identify(this IImageDecoderInternals decoder, Stream stream)
            => decoder.Identify(stream, DefaultLargeImageExceptionFactory);

        public static IImageInfo Identify(
            this IImageDecoderInternals decoder,
            Stream stream,
            Func<InvalidMemoryOperationException, Size, InvalidImageContentException> largeImageExceptionFactory)
        {
            using BufferedReadStream bufferedReadStream = new BufferedReadStream(stream);

            try
            {
                return decoder.Identify(bufferedReadStream, default);
            }
            catch (InvalidMemoryOperationException ex)
            {
                throw largeImageExceptionFactory(ex, decoder.Dimensions);
            }
        }

        public static Image<TPixel> Decode<TPixel>(this IImageDecoderInternals decoder, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
            => decoder.Decode<TPixel>(stream, DefaultLargeImageExceptionFactory);

        public static Image<TPixel> Decode<TPixel>(
            this IImageDecoderInternals decoder,
            Stream stream,
            Func<InvalidMemoryOperationException, Size, InvalidImageContentException> largeImageExceptionFactory)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using BufferedReadStream bufferedReadStream = new BufferedReadStream(stream);

            try
            {
                return decoder.Decode<TPixel>(bufferedReadStream, default);
            }
            catch (InvalidMemoryOperationException ex)
            {
                throw largeImageExceptionFactory(ex, decoder.Dimensions);
            }
        }

        private static InvalidImageContentException DefaultLargeImageExceptionFactory(
            InvalidMemoryOperationException memoryOperationException,
            Size dimensions) =>
            new InvalidImageContentException(dimensions, memoryOperationException);
    }
}
