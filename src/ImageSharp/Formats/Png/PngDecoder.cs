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
    public sealed class PngDecoder : ImageDecoder
    {
        /// <inheritdoc/>
        public override IImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(stream, nameof(stream));

            return new PngDecoderCore(options).Identify(options.Configuration, stream, cancellationToken);
        }

        /// <inheritdoc/>
        public override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(stream, nameof(stream));

            PngDecoderCore decoder = new(options);
            Image<TPixel> image = decoder.Decode<TPixel>(options.Configuration, stream, cancellationToken);

            Resize(options, image);

            return image;
        }

        /// <inheritdoc/>
        public override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(stream, nameof(stream));

            PngDecoderCore decoder = new(options, true);
            IImageInfo info = decoder.Identify(options.Configuration, stream, cancellationToken);
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
                            ? this.Decode<L16>(options, stream, cancellationToken)
                            : this.Decode<La32>(options, stream, cancellationToken);
                    }

                    return !meta.HasTransparency
                        ? this.Decode<L8>(options, stream, cancellationToken)
                        : this.Decode<La16>(options, stream, cancellationToken);

                case PngColorType.Rgb:
                    if (bits == PngBitDepth.Bit16)
                    {
                        return !meta.HasTransparency
                            ? this.Decode<Rgb48>(options, stream, cancellationToken)
                            : this.Decode<Rgba64>(options, stream, cancellationToken);
                    }

                    return !meta.HasTransparency
                        ? this.Decode<Rgb24>(options, stream, cancellationToken)
                        : this.Decode<Rgba32>(options, stream, cancellationToken);

                case PngColorType.Palette:
                    return this.Decode<Rgba32>(options, stream, cancellationToken);

                case PngColorType.GrayscaleWithAlpha:
                    return (bits == PngBitDepth.Bit16)
                    ? this.Decode<La32>(options, stream, cancellationToken)
                    : this.Decode<La16>(options, stream, cancellationToken);

                case PngColorType.RgbWithAlpha:
                    return (bits == PngBitDepth.Bit16)
                    ? this.Decode<Rgba64>(options, stream, cancellationToken)
                    : this.Decode<Rgba32>(options, stream, cancellationToken);

                default:
                    return this.Decode<Rgba32>(options, stream, cancellationToken);
            }
        }
    }
}
