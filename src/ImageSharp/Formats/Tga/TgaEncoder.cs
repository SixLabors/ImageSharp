// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tga;

/// <summary>
/// Image encoder for writing an image to a stream as a Targa true-vision image.
/// </summary>
public sealed class TgaEncoder : AlphaAwareImageEncoder
{
    /// <summary>
    /// Gets the number of bits per pixel.
    /// </summary>
    public TgaBitsPerPixel? BitsPerPixel { get; init; }

    /// <summary>
    /// Gets a value indicating whether no compression or run length compression should be used.
    /// </summary>
    public TgaCompression Compression { get; init; } = TgaCompression.RunLength;

    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        TgaEncoderCore encoder = new(this, image.Configuration.MemoryAllocator);
        encoder.Encode(image, stream, cancellationToken);
    }
}
