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
    public override void Encode<TPixel>(Image<TPixel> image, Stream stream)
    {
        GifEncoderCore encoder = new(image.GetConfiguration(), this);
        encoder.Encode(image, stream);
    }

    /// <inheritdoc/>
    public override Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        GifEncoderCore encoder = new(image.GetConfiguration(), this);
        return encoder.EncodeAsync(image, stream, cancellationToken);
    }
}
