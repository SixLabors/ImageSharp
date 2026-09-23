// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

/// <summary>
/// JPEG XL primaries
/// </summary>
internal enum JxlPrimaries : byte
{
    /// <summary>
    /// Same as ITU-R BT.709
    /// </summary>
    SRgb = 1,

    /// <summary>
    /// Values encoded in separate fields
    /// </summary>
    Custom = 2,

    /// <summary>
    /// ITU-R BT.2020
    /// </summary>
    Bt2020 = 9,

    P3 = 11,
}
