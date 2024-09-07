// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Defines Png constants defined in the specification.
/// </summary>
internal static class PngConstants
{
    /// <summary>
    /// The character encoding to use when reading and writing textual data keywords and text - (Latin-1 ISO-8859-1).
    /// </summary>
    public static readonly Encoding Encoding = Encoding.GetEncoding("ISO-8859-1");

    /// <summary>
    /// The character encoding to use when reading and writing language tags within iTXt chunks - (ASCII 7bit).
    /// </summary>
    public static readonly Encoding LanguageEncoding = Encoding.ASCII;

    /// <summary>
    /// The character encoding to use when reading and writing translated textual data keywords and text - (UTF8).
    /// </summary>
    public static readonly Encoding TranslatedEncoding = Encoding.UTF8;

    /// <summary>
    /// The list of mimetypes that equate to a Png.
    /// </summary>
    public static readonly IEnumerable<string> MimeTypes = ["image/png", "image/apng"];

    /// <summary>
    /// The list of file extensions that equate to a Png.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = ["png", "apng"];

    /// <summary>
    /// The header bytes as a big-endian coded ulong.
    /// </summary>
    public const ulong HeaderValue = 0x89504E470D0A1A0AUL;

    /// <summary>
    /// The dictionary of available color types.
    /// </summary>
    public static readonly Dictionary<PngColorType, byte[]> ColorTypes = new()
    {
        [PngColorType.Grayscale] = [1, 2, 4, 8, 16],
        [PngColorType.Rgb] = [8, 16],
        [PngColorType.Palette] = [1, 2, 4, 8],
        [PngColorType.GrayscaleWithAlpha] = [8, 16],
        [PngColorType.RgbWithAlpha] = [8, 16]
    };

    /// <summary>
    /// The maximum length of keyword in a text chunk is 79 bytes.
    /// </summary>
    public const int MaxTextKeywordLength = 79;

    /// <summary>
    /// The minimum length of a keyword in a text chunk is 1 byte.
    /// </summary>
    public const int MinTextKeywordLength = 1;

    /// <summary>
    /// Gets the header bytes identifying a Png.
    /// </summary>
    public static ReadOnlySpan<byte> HeaderBytes =>
    [
        0x89, // Set the high bit.
        0x50, // P
        0x4E, // N
        0x47, // G
        0x0D, // Line ending CRLF
        0x0A, // Line ending CRLF
        0x1A, // EOF
        0x0A // LF
    ];

    /// <summary>
    /// Gets the keyword of the XMP metadata, encoded in an iTXT chunk.
    /// </summary>
    public static ReadOnlySpan<byte> XmpKeyword =>
    [
        (byte)'X',
        (byte)'M',
        (byte)'L',
        (byte)':',
        (byte)'c',
        (byte)'o',
        (byte)'m',
        (byte)'.',
        (byte)'a',
        (byte)'d',
        (byte)'o',
        (byte)'b',
        (byte)'e',
        (byte)'.',
        (byte)'x',
        (byte)'m',
        (byte)'p'
    ];
}
