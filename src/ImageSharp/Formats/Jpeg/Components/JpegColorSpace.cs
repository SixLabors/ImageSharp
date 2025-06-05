// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <summary>
/// Identifies the colorspace of a Jpeg image.
/// </summary>
internal enum JpegColorSpace
{
    /// <summary>
    /// Color space with 1 component.
    /// </summary>
    Grayscale,

    /// <summary>
    /// Color space with 4 components.
    /// </summary>
    Ycck,

    /// <summary>
    /// Color space with 4 components.
    /// </summary>
    Cmyk,

    /// <summary>
    /// YccK color space with 4 components, used with tiff images, which use jpeg compression.
    /// </summary>
    TiffYccK,

    /// <summary>
    /// Cmyk color space with 4 components, used with tiff images, which use jpeg compression.
    /// </summary>
    TiffCmyk,

    /// <summary>
    /// Color space with 3 components.
    /// </summary>
    RGB,

    /// <summary>
    /// Color space with 3 components.
    /// </summary>
    YCbCr
}
