// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg;

/// <summary>
/// Decoder for generating an image out of a jpeg encoded stream.
/// </summary>
public sealed class JpegDecoder : SpecializedImageDecoder<JpegDecoderOptions>
{
    private JpegDecoder()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static JpegDecoder Instance { get; } = new();

    /// <inheritdoc/>
    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        using JpegDecoderCore decoder = new(new JpegDecoderOptions { GeneralOptions = options });
        return decoder.Identify(options.Configuration, stream, cancellationToken);
    }

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(JpegDecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        using JpegDecoderCore decoder = new(options);
        Image<TPixel> image = decoder.Decode<TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);

        if (options.ResizeMode != JpegDecoderResizeMode.IdctOnly)
        {
            ScaleToTargetSize(options.GeneralOptions, image);
        }

        return image;
    }

    /// <inheritdoc/>
    protected override Image Decode(JpegDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<Rgb24>(options, stream, cancellationToken);

    /// <inheritdoc/>
    protected override JpegDecoderOptions CreateDefaultSpecializedOptions(DecoderOptions options)
        => new() { GeneralOptions = options };
}
