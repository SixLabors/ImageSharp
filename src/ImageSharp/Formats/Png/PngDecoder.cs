// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;
using System.Threading;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Decoder for generating an image out of a png encoded stream.
    /// </summary>
    public sealed class PngDecoder : ImageDecoder<PngDecoderOptions>
    {
        /// <inheritdoc/>
        public override Image<TPixel> DecodeSpecialized<TPixel>(PngDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            PngDecoderCore decoder = new(options);
            Image<TPixel> image = decoder.Decode<PngDecoderOptions, TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);

            Resize(options.GeneralOptions, image);

            return image;
        }

        /// <inheritdoc/>
        public override Image DecodeSpecialized(PngDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            PngDecoderCore decoder = new(options, true);
            IImageInfo info = decoder.Identify(options.GeneralOptions.Configuration, stream, cancellationToken);
            stream.Position = 0;

            PngMetadata meta = info.Metadata.GetPngMetadata();
            PngColorType color = meta.ColorType.GetValueOrDefault();
            PngBitDepth bits = meta.BitDepth.GetValueOrDefault();
            switch (color)
            {
                case PngColorType.Grayscale:
                    if (bits == PngBitDepth.Bit16)
                    {
                        return !meta.HasTransparency
                            ? this.DecodeSpecialized<L16>(options, stream, cancellationToken)
                            : this.DecodeSpecialized<La32>(options, stream, cancellationToken);
                    }

                    return !meta.HasTransparency
                        ? this.DecodeSpecialized<L8>(options, stream, cancellationToken)
                        : this.DecodeSpecialized<La16>(options, stream, cancellationToken);

                case PngColorType.Rgb:
                    if (bits == PngBitDepth.Bit16)
                    {
                        return !meta.HasTransparency
                            ? this.DecodeSpecialized<Rgb48>(options, stream, cancellationToken)
                            : this.DecodeSpecialized<Rgba64>(options, stream, cancellationToken);
                    }

                    return !meta.HasTransparency
                        ? this.DecodeSpecialized<Rgb24>(options, stream, cancellationToken)
                        : this.DecodeSpecialized<Rgba32>(options, stream, cancellationToken);

                case PngColorType.Palette:
                    return this.DecodeSpecialized<Rgba32>(options, stream, cancellationToken);

                case PngColorType.GrayscaleWithAlpha:
                    return (bits == PngBitDepth.Bit16)
                    ? this.DecodeSpecialized<La32>(options, stream, cancellationToken)
                    : this.DecodeSpecialized<La16>(options, stream, cancellationToken);

                case PngColorType.RgbWithAlpha:
                    return (bits == PngBitDepth.Bit16)
                    ? this.DecodeSpecialized<Rgba64>(options, stream, cancellationToken)
                    : this.DecodeSpecialized<Rgba32>(options, stream, cancellationToken);

                default:
                    return this.DecodeSpecialized<Rgba32>(options, stream, cancellationToken);
            }
        }

        /// <inheritdoc/>
        public override IImageInfo IdentifySpecialized(PngDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(stream, nameof(stream));

            return new PngDecoderCore(options).Identify(options.GeneralOptions.Configuration, stream, cancellationToken);
        }
    }
}
