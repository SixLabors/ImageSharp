// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Icon;

/// <summary>
/// Specifies the encoding used for an image embedded in an ICO or CUR resource.
/// </summary>
public enum IconFrameCompression
{
    /// <summary>
    /// The image is encoded as a headerless Windows bitmap with an AND transparency mask.
    /// </summary>
    Bmp,

    /// <summary>
    /// The image is encoded as PNG data.
    /// </summary>
    Png
}
