// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Formats.Tiff;

/// <summary>
/// Encoder for writing the data image to a stream in TIFF format.
/// </summary>
/// <remarks>
/// <para>
/// Explicit TIFF options override each frame's TIFF metadata. Unset options use that metadata or the format defaults.
/// Unsupported combinations use the encoder's supported sample layouts and compression methods.
/// </para>
/// <para>
/// Each frame retains its ICC profile when the profile's data color space matches the output samples.
/// Encoding converts sRGB pixels to that profile's color space. Incompatible profiles are omitted.
/// ICC profiles are retained when <see cref="ImageEncoder.SkipMetadata"/> is true.
/// The source pixels and metadata remain unchanged.
/// </para>
/// <para>
/// For unassociated source pixels, <see cref="TransparentColorMode.Clear"/> clears RGB where alpha is zero.
/// <see cref="TransparentColorMode.Preserve"/> does not clear those values. Color conversion still applies.
/// JPEG-compressed output stores no alpha.
/// </para>
/// </remarks>
public class TiffEncoder : QuantizingImageEncoder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TiffEncoder"/> class.
    /// </summary>
    public TiffEncoder() => this.Quantizer = KnownQuantizers.Hexadecatree;

    /// <summary>
    /// Gets the number of bits per pixel.
    /// </summary>
    public TiffBitsPerPixel? BitsPerPixel { get; init; }

    /// <summary>
    /// Gets the format of the samples written to the TIFF image.
    /// Floating-point samples use 32 bits per component.
    /// </summary>
    public TiffSampleFormat? SampleFormat { get; init; }

    /// <summary>
    /// Gets the compression type to use.
    /// </summary>
    /// <remarks>
    /// JPEG compression supports eight-bit samples: 8-bit BlackIsZero grayscale, 24-bit RGB, and 32-bit Separated CMYK.
    /// Other sample layouts use the encoder's compression fallback.
    /// </remarks>
    public TiffCompression? Compression { get; init; }

    /// <summary>
    /// Gets the compression level 1-9 for the deflate compression mode.
    /// <remarks>Defaults to <see cref="DeflateCompressionLevel.DefaultCompression" />.</remarks>
    /// </summary>
    public DeflateCompressionLevel? CompressionLevel { get; init; }

    /// <summary>
    /// Gets the interpretation of the encoded color samples.
    /// </summary>
    /// <remarks>
    /// Supported layouts include RGB, palette, grayscale, bilevel, and JPEG-compressed CMYK.
    /// For JPEG compression, Rgb selects JPEG RGB, BlackIsZero selects JPEG grayscale, and Separated selects JPEG CMYK.
    /// <see cref="BitsPerPixel"/> must also select a supported layout.
    /// </remarks>
    public TiffPhotometricInterpretation? PhotometricInterpretation { get; init; }

    /// <summary>
    /// Gets a value indicating which horizontal prediction to use. This can improve the compression ratio with deflate or lzw compression.
    /// </summary>
    public TiffPredictor? HorizontalPredictor { get; init; }

    /// <inheritdoc/>
    protected override void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
    {
        TiffEncoderCore encode = new(this, image.Configuration);
        encode.Encode(image, stream, cancellationToken);
    }
}
