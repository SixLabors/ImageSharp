// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Advanced;

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Image encoder for writing an image to a stream as a Windows bitmap.
/// </summary>
public sealed class BmpEncoder : QuantizingImageEncoder
{
    /// <summary>
    /// Gets or sets the number of bits per pixel.
    /// </summary>
    public BmpBitsPerPixel? BitsPerPixel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the encoder should support transparency.
    /// Note: Transparency support only works together with 32 bits per pixel. This option will
    /// change the default behavior of the encoder of writing a bitmap version 3 info header with no compression.
    /// Instead a bitmap version 4 info header will be written with the BITFIELDS compression.
    /// </summary>
    public bool SupportTransparency { get; set; }

    /// <inheritdoc/>
    public override void Encode<TPixel>(Image<TPixel> image, Stream stream)
    {
        BmpEncoderCore encoder = new(this, image.GetMemoryAllocator());
        encoder.Encode(image, stream);
    }

    /// <inheritdoc/>
    public override Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        BmpEncoderCore encoder = new(this, image.GetMemoryAllocator());
        return encoder.EncodeAsync(image, stream, cancellationToken);
    }
}
