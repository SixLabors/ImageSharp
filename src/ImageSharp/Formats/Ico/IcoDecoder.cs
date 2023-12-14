// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Ico;

/// <summary>
/// Decoder for generating an image out of a ico encoded stream.
/// </summary>
public sealed class IcoDecoder : ImageDecoder
{
    private IcoDecoder()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static IcoDecoder Instance { get; } = new();

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        Image<TPixel> image = new IcoDecoderCore(options).Decode<TPixel>(options.Configuration, stream, cancellationToken);

        ScaleToTargetSize(options, image);

        return image;
    }

    /// <inheritdoc/>
    protected override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
            => this.Decode<Rgba32>(options, stream, cancellationToken);

    /// <inheritdoc/>
    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        return new IcoDecoderCore(options).Identify(options.Configuration, stream, cancellationToken);
    }
}
