// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Contains constants which represent JPEG XL container format
/// box types as an integer.
/// </summary>
internal static class JxlBoxTypes
{
    /// <summary>
    /// ftyp
    /// </summary>
    public const int FileType = 0x66747970;

    /// <summary>
    /// jxlc
    /// </summary>
    public const int JxlCodeStream = 0x6A786C63;

    /// <summary>
    /// jxlp
    /// </summary>
    public const int JxlPartialCodeStream = 0x6A786C70;

    /// <summary>
    /// brob
    /// </summary>
    public const int Brob = 0x62726F62;

    /// <summary>
    /// xml 
    /// </summary>
    public const int Xml = 0x786D6C20;

    /// <summary>
    /// Exif
    /// </summary>
    public const int Exif = 0x45786966;

    /// <summary>
    /// jxl 
    /// </summary>
    public const int Jxl = 0x6A786C20;

}
