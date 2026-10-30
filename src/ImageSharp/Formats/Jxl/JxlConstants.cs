// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Contains JPEG XL format identifiers.
/// </summary>
internal static class JxlConstants
{
    /// <summary>
    /// The seventeen quantization-table stream IDs reserved before modular AC groups, including in modular-only frames.
    /// </summary>
    public const int QuantizationTableCount = 17;

    /// <summary>
    /// The maximum encoded byte length of a frame or extra-channel name: 48 plus a 10-bit unsigned value.
    /// </summary>
    public const int MaximumNameByteLength = 1071;

    /// <summary>
    /// The signature box type, "JXL ".
    /// </summary>
    public const uint SignatureBox = 0x4A584C20U;

    /// <summary>
    /// The file type box type, "ftyp".
    /// </summary>
    public const uint FileTypeBox = 0x66747970U;

    /// <summary>
    /// The JPEG XL major brand, "jxl ".
    /// </summary>
    public const uint MajorBrand = 0x6A786C20U;

    /// <summary>
    /// The complete codestream box type, "jxlc".
    /// </summary>
    public const uint CompleteCodestreamBox = 0x6A786C63U;

    /// <summary>
    /// The partial codestream box type, "jxlp".
    /// </summary>
    public const uint PartialCodestreamBox = 0x6A786C70U;

    /// <summary>
    /// The EXIF metadata box type, "Exif".
    /// </summary>
    public const uint ExifBox = 0x45786966U;

    /// <summary>
    /// The XML metadata box type, "xml ".
    /// </summary>
    public const uint XmlBox = 0x786D6C20U;

    /// <summary>
    /// The Brotli-compressed metadata box type, "brob".
    /// </summary>
    public const uint CompressedMetadataBox = 0x62726F62U;

    /// <summary>
    /// The JPEG reconstruction box type, "jbrd".
    /// </summary>
    public const uint JpegReconstructionBox = 0x6A627264U;

    /// <summary>
    /// The codestream level box type, "jxll".
    /// </summary>
    public const uint LevelBox = 0x6A786C6CU;

    /// <summary>
    /// The MIME types used by JPEG XL images.
    /// </summary>
    public static readonly IEnumerable<string> MimeTypes = ["image/jxl"];

    /// <summary>
    /// The JPEG XL filename extensions.
    /// </summary>
    public static readonly IEnumerable<string> FileExtensions = ["jxl"];

    /// <summary>
    /// Gets the signature at the start of a raw codestream.
    /// </summary>
    public static ReadOnlySpan<byte> CodestreamSignature => [0xFF, 0x0A];

    /// <summary>
    /// Gets the complete signature box at the start of a JPEG XL container.
    /// </summary>
    public static ReadOnlySpan<byte> ContainerSignature => [0, 0, 0, 12, 0x4A, 0x58, 0x4C, 0x20, 0x0D, 0x0A, 0x87, 0x0A];
}
