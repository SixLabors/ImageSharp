// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Provides JPEG XL image identification and decoding.
/// </summary>
/// <remarks>
/// The decoder returns image pixels from JPEG reconstruction containers.
/// It skips the data required to recreate the original JPEG file.
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
        // Read only the image header so a later truncated frame remains subject to the typed decoder's recovery policy.
        // The shared core still owns buffering. Restore the initial position so Decode<TPixel> can read the complete image.
        long position = stream.Position;
        JxlDecoderCore decoder = new(options, true);
        ImageInfo info = decoder.Identify(options.GeneralOptions.Configuration, stream, cancellationToken);
        stream.Position = position;

        JxlMetadata metadata = info.Metadata.GetFormatMetadata(JxlFormat.Instance);
        int precision = metadata.BitDepth.BitsPerSample;
        bool floatingPoint = metadata.BitDepth.ExponentBits != 0;
        bool alpha = false;
        bool coloredSpot = false;
        foreach (JxlExtraChannelMetadata channel in metadata.ExtraChannels)
        {
            // Alpha and black contribute displayed components. A rendered spot also contributes color
            // and sample precision, even when the encoded base image has only a luminance channel.
            // Depth and selection masks remain metadata and do not change the destination pixel format.
            if ((channel.Type == JxlExtraChannelType.Alpha && !alpha)
                || channel.Type is JxlExtraChannelType.Black or JxlExtraChannelType.SpotColor)
            {
                alpha |= channel.Type == JxlExtraChannelType.Alpha;
                precision = Math.Max(precision, channel.BitDepth.BitsPerSample);
                floatingPoint |= channel.BitDepth.ExponentBits != 0;

                // Equal spot RGB components retain grayscale. Distinct components need RGB storage;
                // selecting a luminance pixel would discard the color already produced by spot rendering.
                coloredSpot |= channel.Type == JxlExtraChannelType.SpotColor
                    && (channel.SpotColor.X != channel.SpotColor.Y || channel.SpotColor.X != channel.SpotColor.Z);
            }
        }

        if (floatingPoint || precision > 16)
        {
            return this.Decode<RgbaVector>(options, stream, cancellationToken);
        }

        if (metadata.ColorType == JxlColorType.Luminance && !coloredSpot)
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
