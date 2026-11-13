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
    /// </summary>
    public BmpBitsPerPixel? BitsPerPixel { get; init; }

    /// <summary>
    /// Gets the channel layout of 16-bit pixels. The value has an effect only for 16-bit output.
    /// </summary>
    public BmpBit16Layout? Bit16Layout { get; init; }

    /// <summary>
    /// Gets a value indicating whether the encoder should support transparency.
    /// Note: Transparency support only works together with 32 bits per pixel. This option will
    /// change the default behavior of the encoder of writing a bitmap version 3 info header with no compression.
    /// Instead a bitmap version 4 info header will be written with the BITFIELDS compression.
    /// </summary>
    public bool SupportTransparency { get; init; }

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
