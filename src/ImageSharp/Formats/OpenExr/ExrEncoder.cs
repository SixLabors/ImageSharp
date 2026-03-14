// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.OpenExr;

/// <summary>
/// Image encoder for writing an image to a stream in the OpenExr Format.
/// </summary>
public sealed class ExrEncoder : ImageEncoder
{
    /// <summary>
    /// Gets or sets the pixel type of the image.
    /// </summary>
    public ExrPixelType? PixelType { get; set; }

    /// <inheritdoc />
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        ExrEncoderCore encoder = new(this, image.Configuration, image.Configuration.MemoryAllocator);
        encoder.Encode(image, stream, cancellationToken);
    }
}
