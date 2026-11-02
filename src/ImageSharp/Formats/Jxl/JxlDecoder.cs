// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Provides JPEG XL image identification and decoding.
/// </summary>
/// <remarks>
/// Pixel decoding supports final full-canvas modular integer RGB and luminance frames with sRGB primaries and D65.
/// Modular frames can use the sRGB or a gamma transfer curve, reversible color transforms, palettes, squeeze and weighted prediction.
/// VarDCT frames support progressive coefficients, XYB conversion, chroma expansion, adaptive DC smoothing, Gaborish and EPF.
/// VarDCT color and alpha can use twofold, fourfold or eightfold upsampling.
/// LF pyramid frames supply native component samples to later VarDCT frames without intermediate pixel conversion.
/// Both coding modes support one integer alpha channel. Modular upsampling and other image features are not yet supported.
/// </remarks>
public sealed class JxlDecoder : SpecializedImageDecoder<JxlDecoderOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JxlDecoder"/> class.
    /// </summary>
    private JxlDecoder()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static JxlDecoder Instance { get; } = new();

    /// <inheritdoc/>
    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        JxlDecoderCore decoder = new(new JxlDecoderOptions(options));
        return decoder.Identify(options.Configuration, stream, cancellationToken);
    }

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(JxlDecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        JxlDecoderCore decoder = new(options);
        Image<TPixel> image = decoder.Decode<TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);

        ScaleToTargetSize(options.GeneralOptions, image);

        return image;
    }

    /// <inheritdoc/>
    protected override Image Decode(JxlDecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        // The sample precision and alpha channel determine the destination pixel format.
        // Identify reads these fields without allocating pixels. Restore the initial position so Decode<TPixel> can read the complete image.
        long position = stream.Position;
        ImageInfo info = this.Identify(options.GeneralOptions, stream, cancellationToken);
        stream.Position = position;

        JxlMetadata metadata = info.Metadata.GetFormatMetadata(JxlFormat.Instance);
        int precision = metadata.BitDepth.BitsPerSample;
        bool floatingPoint = metadata.BitDepth.ExponentBits != 0;
        bool alpha = false;
        foreach (JxlExtraChannelMetadata channel in metadata.ExtraChannels)
        {
            // Among extra channels, only the first alpha channel and black affect output precision.
            // Other extra channels remain metadata and do not change the destination pixel format.
            if ((channel.Type == JxlExtraChannelType.Alpha && !alpha) || channel.Type == JxlExtraChannelType.Black)
            {
                alpha |= channel.Type == JxlExtraChannelType.Alpha;
                precision = Math.Max(precision, channel.BitDepth.BitsPerSample);
                floatingPoint |= channel.BitDepth.ExponentBits != 0;
            }
        }

        if (floatingPoint || precision > 16)
        {
            return this.Decode<RgbaVector>(options, stream, cancellationToken);
        }

        if (metadata.ColorType == JxlColorType.Luminance)
        {
            return (precision <= 8, alpha) switch
            {
                (true, false) => this.Decode<L8>(options, stream, cancellationToken),
                (true, true) => this.Decode<La16>(options, stream, cancellationToken),
                (false, false) => this.Decode<L16>(options, stream, cancellationToken),
                _ => this.Decode<La32>(options, stream, cancellationToken)
            };
        }

        return (precision <= 8, alpha) switch
        {
            (true, false) => this.Decode<Rgb24>(options, stream, cancellationToken),
            (true, true) => this.Decode<Rgba32>(options, stream, cancellationToken),
            (false, false) => this.Decode<Rgb48>(options, stream, cancellationToken),
            _ => this.Decode<Rgba64>(options, stream, cancellationToken)
        };
    }

    /// <inheritdoc/>
    protected override JxlDecoderOptions CreateDefaultSpecializedOptions(DecoderOptions options) => new(options);
}
