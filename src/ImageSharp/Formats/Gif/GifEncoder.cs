// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Advanced;

namespace SixLabors.ImageSharp.Formats.Gif;

/// <summary>
/// Image encoder for writing image data to a stream in gif format.
/// </summary>
public sealed class GifEncoder : QuantizingImageEncoder
{
    /// <summary>
    /// Gets the color table mode: Global or local.
    /// </summary>
    public GifColorTableMode? ColorTableMode { get; init; }

    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        GifEncoderCore encoder = new(image.Configuration, this);
        encoder.Encode(image, stream, cancellationToken);
    }
}
