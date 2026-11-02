// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

/// <summary>
/// White point from CICP Color Primaries.
/// </summary>
// Note that we define a separate enum instead of using the ColorPrimaries
// enum from CICP code because JPEG XL doesn't support all color primaries defined
// by CICP.
internal enum JxlWhitePoint : byte
{
    /// <summary>
    /// sRGB/ITU-R BT.709/Display P3/ITU-R BT.2020
    /// </summary>
    D65 = 1,

    /// <summary>
    /// Actual values encoded in separate fields
    /// </summary>
    Custom = 2,

    /// <summary>
    /// XYZ
    /// </summary>
    E = 10,

    /// <summary>
    /// DCI-P3
    /// </summary>
    Dci = 11,
}
