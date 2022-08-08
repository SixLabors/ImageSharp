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
    public sealed class PngDecoder : IImageDecoder
    {
        /// <inheritdoc/>
        IImageInfo IImageInfoDetector.Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(stream, nameof(stream));

            return new PngDecoderCore(options).Identify(options.Configuration, stream, cancellationToken);
        }

        /// <inheritdoc/>
        Image<TPixel> IImageDecoder.Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(stream, nameof(stream));

            PngDecoderCore decoder = new(options);
            Image<TPixel> image = decoder.Decode<TPixel>(options.Configuration, stream, cancellationToken);

            ImageDecoderUtilities.Resize(options, image);

            return image;
        }

        /// <inheritdoc/>
        Image IImageDecoder.Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        {
            Guard.NotNull(options, nameof(options));
            Guard.NotNull(stream, nameof(stream));

            PngDecoderCore decoder = new(options, true);
            IImageInfo info = decoder.Identify(options.Configuration, stream, cancellationToken);
            stream.Position = 0;

            PngMetadata meta = info.Metadata.GetPngMetadata();
            PngColorType color = meta.ColorType.GetValueOrDefault();
            PngBitDepth bits = meta.BitDepth.GetValueOrDefault();

            var imageDecoder = (IImageDecoder)this;
            switch (color)
            {
                case PngColorType.Grayscale:
                    if (bits == PngBitDepth.Bit16)
                    {
                        return !meta.HasTransparency
                            ? imageDecoder.Decode<L16>(options, stream, cancellationToken)
                            : imageDecoder.Decode<La32>(options, stream, cancellationToken);
                    }

                    return !meta.HasTransparency
                        ? imageDecoder.Decode<L8>(options, stream, cancellationToken)
                        : imageDecoder.Decode<La16>(options, stream, cancellationToken);

                case PngColorType.Rgb:
                    if (bits == PngBitDepth.Bit16)
                    {
                        return !meta.HasTransparency
                            ? imageDecoder.Decode<Rgb48>(options, stream, cancellationToken)
                            : imageDecoder.Decode<Rgba64>(options, stream, cancellationToken);
                    }

                    return !meta.HasTransparency
                        ? imageDecoder.Decode<Rgb24>(options, stream, cancellationToken)
                        : imageDecoder.Decode<Rgba32>(options, stream, cancellationToken);

                case PngColorType.Palette:
                    return imageDecoder.Decode<Rgba32>(options, stream, cancellationToken);

                case PngColorType.GrayscaleWithAlpha:
                    return (bits == PngBitDepth.Bit16)
                    ? imageDecoder.Decode<La32>(options, stream, cancellationToken)
                    : imageDecoder.Decode<La16>(options, stream, cancellationToken);

                case PngColorType.RgbWithAlpha:
                    return (bits == PngBitDepth.Bit16)
                    ? imageDecoder.Decode<Rgba64>(options, stream, cancellationToken)
                    : imageDecoder.Decode<Rgba32>(options, stream, cancellationToken);

                default:
                    return imageDecoder.Decode<Rgba32>(options, stream, cancellationToken);
            }
        }
    }
}
