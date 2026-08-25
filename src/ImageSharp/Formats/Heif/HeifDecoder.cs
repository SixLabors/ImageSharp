// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Image decoder for reading HEIF images from a stream.
/// </summary>
public sealed class HeifDecoder : ImageDecoder
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

        return new HeifDecoderCore(options).Identify(options.Configuration, stream, cancellationToken);
    }

    /// <inheritdoc />
    protected override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        HeifDecoderCore decoder = new(options);
        Image<TPixel> image = decoder.Decode<TPixel>(options.Configuration, stream, cancellationToken);
        ScaleToTargetSize(options, image);

        return image;
    }

    /// <inheritdoc />
    protected override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<Rgba32>(options, stream, cancellationToken);
}
