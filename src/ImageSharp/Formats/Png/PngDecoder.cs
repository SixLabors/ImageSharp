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
            return color switch
            {
                PngColorType.Grayscale => (bits == PngBitDepth.Bit16)
                ? this.Decode<L16>(configuration, stream)
                : this.Decode<L8>(configuration, stream),

                PngColorType.Rgb => this.Decode<Rgb24>(configuration, stream),

                PngColorType.Palette => this.Decode<Rgba32>(configuration, stream),

                PngColorType.GrayscaleWithAlpha => (bits == PngBitDepth.Bit16)
                ? this.Decode<La32>(configuration, stream)
                : this.Decode<La16>(configuration, stream),

                PngColorType.RgbWithAlpha => (bits == PngBitDepth.Bit16)
                ? this.Decode<Rgba64>(configuration, stream)
                : this.Decode<Rgba32>(configuration, stream),

                _ => this.Decode<Rgba32>(configuration, stream),
            };
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
            return color switch
            {
                PngColorType.Grayscale => (bits == PngBitDepth.Bit16)
                ? await this.DecodeAsync<L16>(configuration, stream, cancellationToken).ConfigureAwait(false)
                : await this.DecodeAsync<L8>(configuration, stream, cancellationToken).ConfigureAwait(false),

                PngColorType.Rgb => await this.DecodeAsync<Rgb24>(configuration, stream, cancellationToken).ConfigureAwait(false),

                PngColorType.Palette => await this.DecodeAsync<Rgba32>(configuration, stream, cancellationToken).ConfigureAwait(false),

                PngColorType.GrayscaleWithAlpha => (bits == PngBitDepth.Bit16)
                ? await this.DecodeAsync<La32>(configuration, stream, cancellationToken).ConfigureAwait(false)
                : await this.DecodeAsync<La16>(configuration, stream, cancellationToken).ConfigureAwait(false),

                PngColorType.RgbWithAlpha => (bits == PngBitDepth.Bit16)
                ? await this.DecodeAsync<Rgba64>(configuration, stream, cancellationToken).ConfigureAwait(false)
                : await this.DecodeAsync<Rgba32>(configuration, stream, cancellationToken).ConfigureAwait(false),

                _ => await this.DecodeAsync<Rgba32>(configuration, stream, cancellationToken).ConfigureAwait(false),
            };
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
