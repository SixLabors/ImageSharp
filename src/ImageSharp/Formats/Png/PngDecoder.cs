// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Decoder for generating an image out of a png encoded stream.
/// </summary>
public sealed class PngDecoder : SpecializedImageDecoder<PngDecoderOptions>
{
    private PngDecoder()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static PngDecoder Instance { get; } = new();

    /// <inheritdoc/>
    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        return new PngDecoderCore(new PngDecoderOptions { GeneralOptions = options }).Identify(options.Configuration, stream, cancellationToken);
    }

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(PngDecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        PngDecoderCore decoder = new(options);
        Image<TPixel> image = decoder.Decode<TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);

        ScaleToTargetSize(options.GeneralOptions, image);

        return image;
    }

    /// <inheritdoc/>
    protected override Image Decode(PngDecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        PngDecoderCore decoder = new(options, true);
        ImageInfo info = decoder.Identify(options.GeneralOptions.Configuration, stream, cancellationToken);
        stream.Position = 0;

        PngMetadata meta = info.Metadata.GetPngMetadata();
        PngColorType color = meta.ColorType;
        PngBitDepth bits = meta.BitDepth;

        switch (color)
        {
            case PngColorType.Grayscale:
                if (bits == PngBitDepth.Bit16)
                {
                    return !meta.TransparentColor.HasValue
                        ? this.Decode<L16>(options, stream, cancellationToken)
                        : this.Decode<La32>(options, stream, cancellationToken);
                }

                return !meta.TransparentColor.HasValue
                    ? this.Decode<L8>(options, stream, cancellationToken)
                    : this.Decode<La16>(options, stream, cancellationToken);

            case PngColorType.Rgb:
                if (bits == PngBitDepth.Bit16)
                {
                    return !meta.TransparentColor.HasValue
                        ? this.Decode<Rgb48>(options, stream, cancellationToken)
                        : this.Decode<Rgba64>(options, stream, cancellationToken);
                }

                return !meta.TransparentColor.HasValue
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

    /// <inheritdoc/>
    protected override PngDecoderOptions CreateDefaultSpecializedOptions(DecoderOptions options) => new() { GeneralOptions = options };
}
