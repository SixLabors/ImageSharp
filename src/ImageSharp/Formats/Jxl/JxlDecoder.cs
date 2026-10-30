// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Provides JPEG XL image identification and decoding.
/// </summary>
/// <remarks>
/// Image identification is supported. Pixel decoding is not yet supported.
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
        => this.Decode<RgbaVector>(options, stream, cancellationToken);

    /// <inheritdoc/>
    protected override JxlDecoderOptions CreateDefaultSpecializedOptions(DecoderOptions options) => new(options);
}
