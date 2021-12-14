// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            PngDecoderCore decoder = new(configuration, this);
            return decoder.Decode<TPixel>(configuration, stream);
        }

        /// <inheritdoc />
        public Image Decode(Configuration configuration, Stream stream)
        {
            PngDecoderCore decoder = new(configuration, true);
            IImageInfo info = decoder.Identify(configuration, stream);
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
                            ? this.Decode<L16>(configuration, stream)
                            : this.Decode<La32>(configuration, stream);
                    }

                    return !meta.HasTransparency
                        ? this.Decode<L8>(configuration, stream)
                        : this.Decode<La16>(configuration, stream);

                case PngColorType.Rgb:
                    if (bits == PngBitDepth.Bit16)
                    {
                        return !meta.HasTransparency
                            ? this.Decode<Rgb48>(configuration, stream)
                            : this.Decode<Rgba64>(configuration, stream);
                    }

                    return !meta.HasTransparency
                        ? this.Decode<Rgb24>(configuration, stream)
                        : this.Decode<Rgba32>(configuration, stream);

                case PngColorType.Palette:
                    return this.Decode<Rgba32>(configuration, stream);

                case PngColorType.GrayscaleWithAlpha:
                    return (bits == PngBitDepth.Bit16)
                    ? this.Decode<La32>(configuration, stream)
                    : this.Decode<La16>(configuration, stream);

                case PngColorType.RgbWithAlpha:
                    return (bits == PngBitDepth.Bit16)
                    ? this.Decode<Rgba64>(configuration, stream)
                    : this.Decode<Rgba32>(configuration, stream);

                default:
                    return this.Decode<Rgba32>(configuration, stream);
            }
        }

        /// <inheritdoc/>
        public Task<Image<TPixel>> DecodeAsync<TPixel>(Configuration configuration, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            PngDecoderCore decoder = new(configuration, this);
            return decoder.DecodeAsync<TPixel>(configuration, stream, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Image> DecodeAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken)
        {
            PngDecoderCore decoder = new(configuration, true);
            IImageInfo info = await decoder.IdentifyAsync(configuration, stream, cancellationToken).ConfigureAwait(false);
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
                            ? await this.DecodeAsync<L16>(configuration, stream, cancellationToken).ConfigureAwait(false)
                            : await this.DecodeAsync<La32>(configuration, stream, cancellationToken).ConfigureAwait(false);
                    }

                    return !meta.HasTransparency
                        ? await this.DecodeAsync<L8>(configuration, stream, cancellationToken).ConfigureAwait(false)
                        : await this.DecodeAsync<La16>(configuration, stream, cancellationToken).ConfigureAwait(false);

                case PngColorType.Rgb:
                    if (bits == PngBitDepth.Bit16)
                    {
                        return !meta.HasTransparency
                            ? await this.DecodeAsync<Rgb48>(configuration, stream, cancellationToken).ConfigureAwait(false)
                            : await this.DecodeAsync<Rgba64>(configuration, stream, cancellationToken).ConfigureAwait(false);
                    }

                    return !meta.HasTransparency
                        ? await this.DecodeAsync<Rgb24>(configuration, stream, cancellationToken).ConfigureAwait(false)
                        : await this.DecodeAsync<Rgba32>(configuration, stream, cancellationToken).ConfigureAwait(false);

                case PngColorType.Palette:
                    return await this.DecodeAsync<Rgba32>(configuration, stream, cancellationToken).ConfigureAwait(false);

                case PngColorType.GrayscaleWithAlpha:
                    return (bits == PngBitDepth.Bit16)
                        ? await this.DecodeAsync<La32>(configuration, stream, cancellationToken).ConfigureAwait(false)
                        : await this.DecodeAsync<La16>(configuration, stream, cancellationToken).ConfigureAwait(false);

                case PngColorType.RgbWithAlpha:
                    return (bits == PngBitDepth.Bit16)
                        ? await this.DecodeAsync<Rgba64>(configuration, stream, cancellationToken).ConfigureAwait(false)
                        : await this.DecodeAsync<Rgba32>(configuration, stream, cancellationToken).ConfigureAwait(false);

                default:
                    return await this.DecodeAsync<Rgba32>(configuration, stream, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public IImageInfo Identify(Configuration configuration, Stream stream)
        {
            PngDecoderCore decoder = new(configuration, this);
            return decoder.Identify(configuration, stream);
        }

        /// <inheritdoc/>
        public Task<IImageInfo> IdentifyAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken)
        {
            PngDecoderCore decoder = new(configuration, this);
            return decoder.IdentifyAsync(configuration, stream, cancellationToken);
        }
    }
}
