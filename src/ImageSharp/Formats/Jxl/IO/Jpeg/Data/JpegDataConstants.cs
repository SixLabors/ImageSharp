// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;

/// <summary>
/// Constants used in parsed JPEG data.
/// </summary>
internal static class JpegDataConstants
{
    /// <summary>
    /// Maximum number of components.
    /// </summary>
    public const int MaxComponents = 4;

    /// <summary>
    /// Maximum number of quantizer tables.
    /// </summary>
    public const int MaximumQuantizationTables = 4;

    /// <summary>
    /// Maximum number of Huffman code tables.
    /// </summary>
    public const int MaxHuffmanTables = 4;

    /// <summary>
    /// Maximum number of bits for a Huffman code.
    /// </summary>
    public const int JpegHuffmanMaxBitLength = 16;

    /// <summary>
    /// Alphabet size for Huffman tables used in the JPEG format.
    /// </summary>
    public const int JpegHuffmanAlphabetSize = 256;

    /// <summary>
    /// Alphabet size for Huffman tables used in the JPEG format.
    /// </summary>
    /// <remarks>
    /// This is specific to the DC coefficients of the Discrete
    /// Cosine Transform.
    /// </remarks>
    public const int JpegDcAlphabetSize = 12;

    /// <summary>
    /// Maximum number of DHT "Define Huffman Tables" markers.
    /// </summary>
    public const int MaxDhtMarkers = 512;

    /// <summary>
    /// Largest value for width OR height.
    /// </summary>
    public const int MaxDimPixels = 65535;

    /// <summary>
    /// Marker that specifies APP1.
    /// </summary>
    public const int App1 = 0xE1;

    /// <summary>
    /// Marker that specifies APP2.
    /// </summary>
    public const int App2 = 0xE2;

    /// <summary>
    /// Gets the tag bytes specifying the ICC profile.
    /// </summary>
    public static ReadOnlySpan<byte> IccProfileTag => "ICC_PROFILE\0"u8;

    /// <summary>
    /// Gets the tag bytes specifying the EXIF profile.
    /// </summary>
    public static ReadOnlySpan<byte> ExifTag => "Exif\0\0"u8;

    /// <summary>
    /// Gets the tag bytes specifying the XMP profile.
    /// </summary>
    public static ReadOnlySpan<byte> XmpTag => "http://ns.adobe.com/xap/1.0/\0"u8;

    public static ReadOnlySpan<int> JpegNaturalOrder =>
    [
        0,   1,  8, 16,  9,  2,  3, 10,
        17, 24, 32, 25, 18, 11,  4,  5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13,  6,  7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63,
        63, 63, 63, 63, 63, 63, 63, 63,
        63, 63, 63, 63, 63, 63, 63, 63
    ];

    public static ReadOnlySpan<int> JpegZigZagOrder =>
    [
        0,   1,  5,  6, 14, 15, 27, 28,
        2,   4,  7, 13, 16, 26, 29, 42,
        3,   8, 12, 17, 25, 30, 41, 43,
        9,  11, 18, 24, 31, 40, 44, 53,
        10, 19, 23, 32, 39, 45, 52, 54,
        20, 22, 33, 38, 46, 51, 55, 60,
        21, 34, 37, 47, 50, 56, 59, 61,
        35, 36, 48, 49, 57, 58, 62, 63
    ];
}
