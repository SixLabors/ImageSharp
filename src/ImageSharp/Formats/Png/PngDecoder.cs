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
    public sealed class PngDecoder : IImageDecoder, IPngDecoderOptions, IImageInfoDetector
    {
        /// <inheritdoc/>
        public bool IgnoreMetadata { get; set; }

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            PngDecoderCore decoder = new(configuration, this);
            return decoder.Decode<TPixel>(configuration, stream, cancellationToken);
        }

        /// <inheritdoc />
        public Image Decode(Configuration configuration, Stream stream, CancellationToken cancellationToken)
        {
            PngDecoderCore decoder = new(configuration, true);
            IImageInfo info = decoder.Identify(configuration, stream, cancellationToken);
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
                            ? this.Decode<L16>(configuration, stream, cancellationToken)
                            : this.Decode<La32>(configuration, stream, cancellationToken);
                    }

                    return !meta.HasTransparency
                        ? this.Decode<L8>(configuration, stream, cancellationToken)
                        : this.Decode<La16>(configuration, stream, cancellationToken);

                case PngColorType.Rgb:
                    if (bits == PngBitDepth.Bit16)
                    {
                        return !meta.HasTransparency
                            ? this.Decode<Rgb48>(configuration, stream, cancellationToken)
                            : this.Decode<Rgba64>(configuration, stream, cancellationToken);
                    }

                    return !meta.HasTransparency
                        ? this.Decode<Rgb24>(configuration, stream, cancellationToken)
                        : this.Decode<Rgba32>(configuration, stream, cancellationToken);

                case PngColorType.Palette:
                    return this.Decode<Rgba32>(configuration, stream, cancellationToken);

                case PngColorType.GrayscaleWithAlpha:
                    return (bits == PngBitDepth.Bit16)
                    ? this.Decode<La32>(configuration, stream, cancellationToken)
                    : this.Decode<La16>(configuration, stream, cancellationToken);

                case PngColorType.RgbWithAlpha:
                    return (bits == PngBitDepth.Bit16)
                    ? this.Decode<Rgba64>(configuration, stream, cancellationToken)
                    : this.Decode<Rgba32>(configuration, stream, cancellationToken);

                default:
                    return this.Decode<Rgba32>(configuration, stream, cancellationToken);
            }
        }

        /// <inheritdoc/>
        public IImageInfo Identify(Configuration configuration, Stream stream, CancellationToken cancellationToken)
        {
            PngDecoderCore decoder = new(configuration, this);
            return decoder.Identify(configuration, stream, cancellationToken);
        }
    }
}
