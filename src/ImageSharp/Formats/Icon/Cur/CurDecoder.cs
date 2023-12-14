// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Icon.Cur;

/// <summary>
/// Decoder for generating an image out of a ico encoded stream.
/// </summary>
public sealed class CurDecoder : ImageDecoder
{
    private CurDecoder()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static CurDecoder Instance { get; } = new();

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        Image<TPixel> image = new CurDecoderCore(options).Decode<TPixel>(options.Configuration, stream, cancellationToken);

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

        return new CurDecoderCore(options).Identify(options.Configuration, stream, cancellationToken);
    }
}
