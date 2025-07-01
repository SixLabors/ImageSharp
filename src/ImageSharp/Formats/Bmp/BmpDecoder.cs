// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Image decoder for generating an image out of a Windows bitmap stream.
/// </summary>
public sealed class BmpDecoder : SpecializedImageDecoder<BmpDecoderOptions>
{
    private BmpDecoder()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static BmpDecoder Instance { get; } = new();

    /// <inheritdoc/>
    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        return new BmpDecoderCore(new BmpDecoderOptions { GeneralOptions = options }).Identify(options.Configuration, stream, cancellationToken);
    }

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(BmpDecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        Image<TPixel> image = new BmpDecoderCore(options).Decode<TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);

        ScaleToTargetSize(options.GeneralOptions, image);

        return image;
    }

    /// <inheritdoc/>
    protected override Image Decode(BmpDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<Rgba32>(options, stream, cancellationToken);

    /// <inheritdoc/>
    protected override BmpDecoderOptions CreateDefaultSpecializedOptions(DecoderOptions options)
        => new() { GeneralOptions = options };
}
