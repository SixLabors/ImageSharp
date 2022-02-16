// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Image decoder for generating an image out of a webp stream.
    /// </summary>
    public sealed class WebpDecoder : IImageDecoder, IWebpDecoderOptions, IImageInfoDetector
    {
        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; set; }

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream, CancellationToken cancellationToken = default)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(stream, nameof(stream));

            var decoder = new WebpDecoderCore(configuration, this);

            try
            {
                return decoder.Decode<TPixel>(configuration, stream, cancellationToken);
            }
            catch (InvalidMemoryOperationException ex)
            {
                Size dims = decoder.Dimensions;

                throw new InvalidImageContentException($"Cannot decode image. Failed to allocate buffers for possibly degenerate dimensions: {dims.Width}x{dims.Height}.", ex);
            }
        }

        /// <inheritdoc/>
        public IImageInfo Identify(Configuration configuration, Stream stream, CancellationToken cancellationToken = default)
        {
            Guard.NotNull(stream, nameof(stream));

            return new WebpDecoderCore(configuration, this).Identify(configuration, stream, cancellationToken);
        }

        /// <inheritdoc />
        public Image Decode(Configuration configuration, Stream stream, CancellationToken cancellationToken = default) => this.Decode<Rgba32>(configuration, stream, cancellationToken);
    }
}
