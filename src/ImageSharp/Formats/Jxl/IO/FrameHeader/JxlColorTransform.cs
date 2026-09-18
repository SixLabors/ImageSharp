// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;

/// <summary>
/// Represents the type of JPEG XL color transform.
/// </summary>
internal enum JxlColorTransform : byte
{
    /// <summary>
    /// Use XYB encoding
    /// </summary>
    Xyb,

    /// <summary>
    /// Encode according to the attached color profile.
    /// </summary>
    None,

    /// <summary>
    /// Encode according to the attached color profile but
    /// transformed into Y'Cb'Cr.
    /// </summary>
    YCbCr,
}
