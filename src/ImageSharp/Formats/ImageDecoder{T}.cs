// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// The base class for all image decoders.
    /// </summary>
    /// <typeparam name="T">The type of specialized decoder options.</typeparam>
    public abstract class ImageDecoder<T> : IImageInfoDetector2, IImageDecoder2
        where T : ISpecializedDecoderOptions, new()
    {
        /// <inheritdoc/>
        public IImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            T specializedOptions = new() { GeneralOptions = options };
            return this.IdentifySpecialized(specializedOptions, stream, cancellationToken);
        }

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            T specializedOptions = new() { GeneralOptions = options };
            Image<TPixel> image = this.DecodeSpecialized<TPixel>(specializedOptions, stream, cancellationToken);

            Resize(options, image);

            return image;
        }

        /// <inheritdoc/>
        public Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            T specializedOptions = new() { GeneralOptions = options };
            Image image = this.DecodeSpecialized(specializedOptions, stream, cancellationToken);

            Resize(options, image);

            return image;
        }

        /// <summary>
        /// Reads the raw image information from the specified stream.
        /// </summary>
        /// <param name="options">The specialized decoder options.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The <see cref="IImageInfo"/> object.</returns>
        /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
        public abstract IImageInfo IdentifySpecialized(T options, Stream stream, CancellationToken cancellationToken);

        /// <summary>
        /// Decodes the image from the specified stream to an <see cref="Image{TPixel}"/> of a specific pixel type.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="options">The specialized decoder options.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
        public abstract Image<TPixel> DecodeSpecialized<TPixel>(T options, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>;

        /// <summary>
        /// Decodes the image from the specified stream to an <see cref="Image"/> of a specific pixel type.
        /// </summary>
        /// <param name="options">The specialized decoder options.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        /// <exception cref="ImageFormatException">Thrown if the encoded image contains errors.</exception>
        public abstract Image DecodeSpecialized(T options, Stream stream, CancellationToken cancellationToken);

        /// <summary>
        /// Performs a resize operation against the decoded image. If the target size is not set, or the image size
        /// already matches the target size, the image is untouched.
        /// </summary>
        /// <param name="options">The decoder options.</param>
        /// <param name="image">The decoded image.</param>
        protected static void Resize(DecoderOptions options, Image image)
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
    }
}
