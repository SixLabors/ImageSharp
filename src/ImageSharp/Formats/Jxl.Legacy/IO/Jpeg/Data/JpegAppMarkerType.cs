// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg.Data;

/// <summary>
/// Identifies the kind of APP marker in a JPEG file.
/// </summary>
internal enum JpegAppMarkerType : byte
{
    /// <summary>
    /// Unknown APP marker
    /// </summary>
    Unknown,

    /// <summary>
    /// Contains ICC profile metadata
    /// </summary>
    Icc,

    /// <summary>
    /// Contains EXIF profile metadata
    /// </summary>
    Exif,

    /// <summary>
    /// Contains XMP profile metadata
    /// </summary>
    Xmp
}
