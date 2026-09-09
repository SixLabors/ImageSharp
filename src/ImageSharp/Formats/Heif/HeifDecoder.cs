// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Image decoder for reading HEIF images from a stream.
/// </summary>
public sealed class HeifDecoder : SpecializedImageDecoder<HeifDecoderOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifDecoder"/> class.
    /// </summary>
    private HeifDecoder()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static HeifDecoder Instance { get; } = new();

    /// <inheritdoc/>
    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        return new HeifDecoderCore(new HeifDecoderOptions { GeneralOptions = options }).Identify(options.Configuration, stream, cancellationToken);
    }

    /// <inheritdoc />
    protected override Image<TPixel> Decode<TPixel>(HeifDecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        HeifDecoderCore decoder = new(options);
        Image<TPixel> image = decoder.Decode<TPixel>(options.GeneralOptions.Configuration, stream, cancellationToken);
        ScaleToTargetSize(options.GeneralOptions, image);

        return image;
    }

    /// <inheritdoc />
    protected override Image Decode(HeifDecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<Rgba32>(options, stream, cancellationToken);

    /// <inheritdoc/>
    protected override HeifDecoderOptions CreateDefaultSpecializedOptions(DecoderOptions options)
        => new() { GeneralOptions = options };
}
