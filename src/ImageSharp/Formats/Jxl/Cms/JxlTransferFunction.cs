// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

/// <summary>
/// JPEG XL transfer function type
/// </summary>
internal enum JxlTransferFunction : byte
{
    /// <summary>
    /// ITU-R BT.709
    /// </summary>
    Bt709 = 1,

    /// <summary>
    /// Unknown transfer function
    /// </summary>
    Unknown = 2,

    /// <summary>
    /// Linear transfer function
    /// </summary>
    Linear = 8,

    /// <summary>
    /// sRGB
    /// </summary>
    SRgb = 13,

    /// <summary>
    /// From ITU-R BT.2100
    /// </summary>
    Pq = 16,

    /// <summary>
    /// From SMPTE RP 431-2 reference projector
    /// </summary>
    Dci = 17,

    /// <summary>
    /// From ITU-R BT.2100
    /// </summary>
    Hlg = 18,
}
