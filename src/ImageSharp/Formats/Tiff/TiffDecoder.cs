// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Image decoder for generating an image out of a TIFF stream.
    /// </summary>
    public class TiffDecoder : IImageDecoder, ITiffDecoderOptions, IImageInfoDetector
    {
        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; set; }

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(stream, "stream");

            var decoder = new TiffDecoderCore(configuration, this);
            return decoder.Decode<TPixel>(configuration, stream);
        }

        /// <inheritdoc/>
        public Image Decode(Configuration configuration, Stream stream) => this.Decode<Rgba32>(configuration, stream);

        /// <inheritdoc/>
        public Task<Image<TPixel>> DecodeAsync<TPixel>(Configuration configuration, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(stream, nameof(stream));

            var decoder = new TiffDecoderCore(configuration, this);
            return decoder.DecodeAsync<TPixel>(configuration, stream, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<Image> DecodeAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken)
          => await this.DecodeAsync<Rgba32>(configuration, stream, cancellationToken)
            .ConfigureAwait(false);

        /// <inheritdoc/>
        public IImageInfo Identify(Configuration configuration, Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            var decoder = new TiffDecoderCore(configuration, this);
            return decoder.Identify(configuration, stream);
        }

        /// <inheritdoc/>
        public Task<IImageInfo> IdentifyAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(stream, nameof(stream));

            var decoder = new TiffDecoderCore(configuration, this);
            return decoder.IdentifyAsync(configuration, stream, cancellationToken);
        }
    }
}
