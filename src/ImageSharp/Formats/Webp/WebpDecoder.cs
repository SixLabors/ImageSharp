// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Image decoder for generating an image out of a webp stream.
/// </summary>
public sealed class WebpDecoder : ImageDecoder
{
    /// <inheritdoc/>
    protected internal override IImageInfo Identify(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        using WebpDecoderCore decoder = new(options);
        return decoder.Identify(options.Configuration, stream, cancellationToken);
    }

    /// <inheritdoc/>
    protected internal override Image<TPixel> Decode<TPixel>(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        Guard.NotNull(options, nameof(options));
        Guard.NotNull(stream, nameof(stream));

        using WebpDecoderCore decoder = new(options);
        Image<TPixel> image = decoder.Decode<TPixel>(options.Configuration, stream, cancellationToken);

        Resize(options, image);

        return image;
    }

    /// <inheritdoc/>
    protected internal override Image Decode(DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => this.Decode<Rgba32>(options, stream, cancellationToken);
}
