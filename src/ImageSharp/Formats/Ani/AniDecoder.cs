// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Ani;

internal sealed class AniDecoder : ImageDecoder
{
    private AniDecoder()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static AniDecoder Instance { get; } = new();

    protected override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));
        Image<TPixel> image = new AniDecoderCore(options).Decode<TPixel>(options.Configuration, stream, cancellationToken);
        ScaleToTargetSize(options, image);
        return image;
    }

    protected override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken) => this.Decode<Rgba32>(options, stream, cancellationToken);

    protected override ImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));
        return new AniDecoderCore(options).Identify(options.Configuration, stream, cancellationToken);
    }
}
