// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Image decoder for generating an image out of a Windows bitmap stream.
    /// </summary>
    public class BmpDecoder : ImageDecoder, IImageDecoderSpecialized<BmpDecoderOptions>
    {
        /// <inheritdoc/>
        public override IImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(stream, nameof(stream));

            return new BmpDecoderCore(new() { GeneralOptions = options }).Identify(options.Configuration, stream, cancellationToken);
        }

        /// <inheritdoc/>
        public override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => this.DecodeSpecialized<TPixel>(new() { GeneralOptions = options }, stream, cancellationToken);

        /// <inheritdoc/>
        public override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => this.DecodeSpecialized(new() { GeneralOptions = options }, stream, cancellationToken);

        /// <inheritdoc/>
        public Image<TPixel> DecodeSpecialized<TPixel>(BmpDecoderOptions options, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(stream, nameof(stream));

            Image<TPixel> image = new BmpDecoderCore(options).Decode<TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);

            Resize(options.GeneralOptions, image);

            return image;
        }

        /// <inheritdoc/>
        public Image DecodeSpecialized(BmpDecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => this.DecodeSpecialized<Rgba32>(options, stream, cancellationToken);
    }
}
