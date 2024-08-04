// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Info about the webp file format used.
/// </summary>
public enum WebpFileFormatType
{
    /// <summary>
    /// The lossless Webp format, which compresses data without any loss of information.
    /// </summary>
    Lossless,

    /// <summary>
    /// The lossy Webp format, which compresses data by discarding some of it.
    /// </summary>
    Lossy,
}
