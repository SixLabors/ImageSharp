// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.Constants;

/// <summary>
/// Defines constants defined in the TIFF specification.
/// </summary>
internal static class TiffConstants
{
    /// <summary>
    /// Byte order markers for indicating little endian encoding.
    /// </summary>
    public const byte ByteOrderLittleEndian = 0x49;

    /// <summary>
    /// Byte order markers for indicating big endian encoding.
    /// </summary>
    public const byte ByteOrderBigEndian = 0x4D;

    /// <summary>
    /// Byte order markers for indicating little endian encoding.
    /// </summary>
    public const ushort ByteOrderLittleEndianShort = 0x4949;

    /// <summary>
    /// Byte order markers for indicating big endian encoding.
    /// </summary>
    public const ushort ByteOrderBigEndianShort = 0x4D4D;

    /// <summary>
    /// Magic number used within the image file header to identify a TIFF format file.
    /// </summary>
    public const ushort HeaderMagicNumber = 42;

    /// <summary>
    /// The big tiff header magic number
    /// </summary>
    public const ushort BigTiffHeaderMagicNumber = 43;

    /// <summary>
    /// The big tiff byte size of offsets value.
    /// </summary>
    public const ushort BigTiffByteSize = 8;

    /// <summary>
    /// RowsPerStrip default value, which is effectively infinity.
    /// </summary>
    public const int RowsPerStripInfinity = 2147483647;

    /// <summary>
    /// Size (in bytes) of the Rational and SRational data types
    /// </summary>
    public const int SizeOfRational = 8;

    /// <summary>
    /// The default strip size is 8k.
    /// </summary>
    public const int DefaultStripSize = 8 * 1024;

    /// <summary>
    /// The default predictor is None.
    /// </summary>
    public const TiffPredictor DefaultPredictor = TiffPredictor.None;

    /// <summary>
    /// The default bits per pixel is Bit24.
    /// </summary>
    public const TiffBitsPerPixel DefaultBitsPerPixel = TiffBitsPerPixel.Bit24;

    /// <summary>
    /// The default bits per sample for color images with 8 bits for each color channel.
    /// </summary>
    public static readonly TiffBitsPerSample DefaultBitsPerSample = BitsPerSampleRgb8Bit;

    /// <summary>
    /// The default compression is None.
    /// </summary>
    public const TiffCompression DefaultCompression = TiffCompression.None;

    /// <summary>
    /// The default photometric interpretation is Rgb.
    /// </summary>
    public const TiffPhotometricInterpretation DefaultPhotometricInterpretation = TiffPhotometricInterpretation.Rgb;

    /// <summary>
    /// The bits per sample for 1 bit bicolor images.
    /// </summary>
    public static readonly TiffBitsPerSample BitsPerSample1Bit = new(1, 0, 0);

    /// <summary>
    /// The bits per sample for images with a 4 color palette.
    /// </summary>
    public static readonly TiffBitsPerSample BitsPerSample4Bit = new(4, 0, 0);

    /// <summary>
    /// The bits per sample for 8 bit images.
    /// </summary>
    public static readonly TiffBitsPerSample BitsPerSample8Bit = new(8, 0, 0);

    /// <summary>
    /// The bits per sample for 16-bit grayscale images.
    /// </summary>
    public static readonly TiffBitsPerSample BitsPerSample16Bit = new(16, 0, 0);

    /// <summary>
    /// The bits per sample for color images with 8 bits for each color channel.
    /// </summary>
    public static readonly TiffBitsPerSample BitsPerSampleRgb8Bit = new(8, 8, 8);

    /// <summary>
    /// The list of mime types that equate to a tiff.
    /// </summary>
    public static readonly IEnumerable<string> MimeTypes = ["image/tiff", "image/tiff-fx"];

    /// <summary>
    /// The list of file extensions that equate to a tiff.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = ["tiff", "tif"];
}
