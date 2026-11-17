// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Image encoder for writing an image to a stream as a Windows bitmap.
/// </summary>
public sealed class BmpEncoder : QuantizingImageEncoder
{
    /// <summary>
    /// Gets the number of bits per pixel.
    /// Not all values are allowed for all <see cref="ColorType" /> values.
    /// </summary>
    public BmpBitsPerPixel? BitsPerPixel { get; init; }

    /// <summary>
    /// Gets the color type.
    /// </summary>
    public BmpColorType? ColorType { get; init; }

    /// <summary>
    /// Gets the compression of the pixel data. If the value is <see langword="null"/>, the encoder uses
    /// <see cref="BmpMetadata.Compression"/>.
    /// </summary>
    public BmpCompression? Compression { get; init; }

    /// <inheritdoc cref="BmpDecoderOptions.ProcessedAlphaMask"/>
    internal bool ProcessedAlphaMask { get; init; }

    /// <inheritdoc cref="BmpDecoderOptions.SkipFileHeader"/>
    internal bool SkipFileHeader { get; init; }

    /// <inheritdoc cref="BmpDecoderOptions.UseDoubleHeight"/>
    internal bool UseDoubleHeight { get; init; }

    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        BmpEncoderCore encoder = new(this, image.Configuration.MemoryAllocator);
        encoder.Encode(image, stream, cancellationToken);
    }
}
