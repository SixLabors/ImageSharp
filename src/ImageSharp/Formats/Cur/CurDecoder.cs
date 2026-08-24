// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Cur;

/// <summary>
/// Decoder for generating an image from a CUR encoded stream.
/// </summary>
public sealed class CurDecoder : ImageDecoder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CurDecoder"/> class.
    /// </summary>
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
