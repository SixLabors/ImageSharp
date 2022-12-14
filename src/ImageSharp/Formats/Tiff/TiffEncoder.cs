// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Tiff.Constants;

namespace SixLabors.ImageSharp.Formats.Tiff;

/// <summary>
/// Encoder for writing the data image to a stream in TIFF format.
/// </summary>
public class TiffEncoder : QuantizingImageEncoder
{
    /// <summary>
    /// Gets the number of bits per pixel.
    /// </summary>
    public TiffBitsPerPixel? BitsPerPixel { get; init; }

    /// <summary>
    /// Gets the compression type to use.
    /// </summary>
    public TiffCompression? Compression { get; init; }

    /// <summary>
    /// Gets the compression level 1-9 for the deflate compression mode.
    /// <remarks>Defaults to <see cref="DeflateCompressionLevel.DefaultCompression" />.</remarks>
    /// </summary>
    public DeflateCompressionLevel? CompressionLevel { get; init; }

    /// <summary>
    /// Gets the PhotometricInterpretation to use. Possible options are RGB, RGB with a color palette, gray or BiColor.
    /// If no PhotometricInterpretation is specified or it is unsupported by the encoder, RGB will be used.
    /// </summary>
    public TiffPhotometricInterpretation? PhotometricInterpretation { get; init; }

    /// <summary>
    /// Gets a value indicating which horizontal prediction to use. This can improve the compression ratio with deflate or lzw compression.
    /// </summary>
    public TiffPredictor? HorizontalPredictor { get; init; }

    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        TiffEncoderCore encode = new(this, image.GetMemoryAllocator());
        encode.Encode(image, stream, cancellationToken);
    }
}
