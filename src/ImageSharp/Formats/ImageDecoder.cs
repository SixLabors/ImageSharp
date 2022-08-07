// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// The base class for all image decoders.
    /// </summary>
    public abstract class ImageDecoder : IImageInfoDetector, IImageDecoder
    {
        /// <inheritdoc/>
        public abstract IImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken);

        /// <inheritdoc/>
        public abstract Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>;

        /// <inheritdoc/>
        public abstract Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken);

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
