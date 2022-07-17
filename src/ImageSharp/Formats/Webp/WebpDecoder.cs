// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Image decoder for generating an image out of a webp stream.
    /// </summary>
    public sealed class WebpDecoder : ImageDecoder<WebpDecoderOptions>
    {
        /// <inheritdoc/>
        public override Image<TPixel> DecodeSpecialized<TPixel>(WebpDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            WebpDecoderCore decoder = new(options);
            Image<TPixel> image = decoder.Decode<WebpDecoderOptions, TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);

            Resize(options.GeneralOptions, image);

            return image;
        }

        /// <inheritdoc/>
        public override Image DecodeSpecialized(WebpDecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => this.DecodeSpecialized<Rgba32>(options, stream, cancellationToken);

        /// <inheritdoc/>
        public override IImageInfo IdentifySpecialized(WebpDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(stream, nameof(stream));

            return new WebpDecoderCore(options).Identify(options.GeneralOptions.Configuration, stream, cancellationToken);
        }
    }
}
