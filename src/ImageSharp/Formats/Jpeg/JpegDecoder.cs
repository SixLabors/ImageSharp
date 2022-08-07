// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Decoder for generating an image out of a jpeg encoded stream.
    /// </summary>
    public sealed class JpegDecoder : ImageDecoder, IImageDecoderSpecialized<JpegDecoderOptions>
    {
        /// <inheritdoc/>
        public override IImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(stream, nameof(stream));

            using JpegDecoderCore decoder = new(new() { GeneralOptions = options });
            return decoder.Identify(options.Configuration, stream, cancellationToken);
        }

        /// <inheritdoc/>
        public override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => this.DecodeSpecialized<TPixel>(new() { GeneralOptions = options }, stream, cancellationToken);

        /// <inheritdoc/>
        public override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => this.DecodeSpecialized(new() { GeneralOptions = options }, stream, cancellationToken);

        /// <inheritdoc/>
        public Image<TPixel> DecodeSpecialized<TPixel>(JpegDecoderOptions options, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(stream, nameof(stream));

            using JpegDecoderCore decoder = new(options);
            Image<TPixel> image = decoder.Decode<TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);

            if (options.ResizeMode != JpegDecoderResizeMode.IdctOnly)
            {
                Resize(options.GeneralOptions, image);
            }

            return image;
        }

        /// <inheritdoc/>
        public Image DecodeSpecialized(JpegDecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => this.DecodeSpecialized<Rgb24>(options, stream, cancellationToken);
    }
}
