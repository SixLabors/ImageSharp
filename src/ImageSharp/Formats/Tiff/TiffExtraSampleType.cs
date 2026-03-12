// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff;

/// <summary>
/// Description of extra components.
/// </summary>
internal enum TiffExtraSampleType
{
    /// <summary>
    /// The data is unspecified, not supported.
    /// </summary>
    UnspecifiedData = 0,

    /// <summary>
    /// The extra data is associated alpha data (with pre-multiplied color).
    /// </summary>
    AssociatedAlphaData = 1,

    /// <summary>
    /// The extra data is unassociated alpha data is transparency information that logically exists independent of an image;
    /// it is commonly called a soft matte.
    /// </summary>
    UnassociatedAlphaData = 2,

    /// <summary>
    /// A CorelDRAW-specific value observed in damaged files, indicating unassociated alpha.
    /// Not part of the official TIFF specification; patched in ImageSharp for compatibility.
    /// </summary>
    CorelDrawUnassociatedAlphaData = 999,
}
