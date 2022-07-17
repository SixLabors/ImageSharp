// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tga
{
    /// <summary>
    /// Image decoder for Truevision TGA images.
    /// </summary>
    public sealed class TgaDecoder : ImageDecoder<TgaDecoderOptions>
    {
        /// <inheritdoc/>
        public override Image<TPixel> DecodeSpecialized<TPixel>(TgaDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            TgaDecoderCore decoder = new(options);
            Image<TPixel> image = decoder.Decode<TgaDecoderOptions, TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);

            Resize(options.GeneralOptions, image);

            return image;
        }

        /// <inheritdoc/>
        public override Image DecodeSpecialized(TgaDecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => this.DecodeSpecialized<Rgba32>(options, stream, cancellationToken);

        /// <inheritdoc/>
        public override IImageInfo IdentifySpecialized(TgaDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(stream, nameof(stream));

            return new TgaDecoderCore(options).Identify(options.GeneralOptions.Configuration, stream, cancellationToken);
        }
    }
}
