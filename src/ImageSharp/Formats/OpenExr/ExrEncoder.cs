// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.OpenExr;

/// <summary>
/// Image encoder for writing an image to a stream in the OpenExr Format.
/// </summary>
public sealed class ExrEncoder : IImageEncoder, IExrEncoderOptions
{
    /// <summary>
    /// Gets or sets the pixel type of the image.
    /// </summary>
    public ExrPixelType? PixelType { get; set; }

    /// <inheritdoc/>
    public void Encode<TPixel>(Image<TPixel> image, Stream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ExrEncoderCore encoder = new(this, image.GetMemoryAllocator());
        encoder.Encode(image, stream);
    }

    /// <inheritdoc/>
    public Task EncodeAsync<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ExrEncoderCore encoder = new(this, image.GetMemoryAllocator());
        return encoder.EncodeAsync(image, stream, cancellationToken);
    }
}
