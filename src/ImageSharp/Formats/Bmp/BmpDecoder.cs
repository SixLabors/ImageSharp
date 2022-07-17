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
    public class BmpDecoder : ImageDecoder<BmpDecoderOptions>
    {
        /// <inheritdoc/>
        public override Image<TPixel> DecodeSpecialized<TPixel>(BmpDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(stream, nameof(stream));

            BmpDecoderCore decoder = new(options);
            Image<TPixel> image = decoder.Decode<BmpDecoderOptions, TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);

            Resize(options.GeneralOptions, image);

            return image;
        }

        /// <inheritdoc/>
        public override Image DecodeSpecialized(BmpDecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => this.DecodeSpecialized<Rgba32>(options, stream, cancellationToken);

        /// <inheritdoc/>
        public override IImageInfo IdentifySpecialized(BmpDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(stream, nameof(stream));

            return new BmpDecoderCore(options).Identify(options.GeneralOptions.Configuration, stream, cancellationToken);
        }
    }
}
