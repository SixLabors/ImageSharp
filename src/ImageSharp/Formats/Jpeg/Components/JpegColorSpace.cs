// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <summary>
/// Selects the component arithmetic and stored sample convention used by the JPEG color converters.
/// </summary>
/// <remarks>
/// The decoder and encoder share these conversion models. Chroma sampling is configured separately in the JPEG frame.
/// The public <see cref="JpegColorType"/> includes YCbCr sampling ratios and standalone Adobe CMYK/YCCK choices.
/// This internal enum has one YCbCr model and distinct TIFF CMYK/YCCK models because their stored ink conventions differ.
/// Container callers must select the matching internal model so JPEG reads or writes the correct ink values.
/// </remarks>
internal enum JpegColorSpace
{
    /// <summary>
    /// One luminance component.
    /// </summary>
    Luminance,

    /// <summary>
    /// Four Adobe YCCK components. The YCbCr matrix represents CMY inks, and the stored black component is inverted.
    /// </summary>
    Ycck,

    /// <summary>
    /// Four Adobe CMYK components. Zero means full ink and the maximum sample value means no ink.
    /// </summary>
    Cmyk,

    /// <summary>
    /// Four TIFF YCCK components. The YCbCr matrix represents the complements of CMY inks; black retains its conventional value.
    /// Zero means no black ink and the maximum sample value means full black ink.
    /// </summary>
    TiffYccK,

    /// <summary>
    /// Four conventional TIFF CMYK components. Zero means no ink and the maximum sample value means full ink.
    /// </summary>
    TiffCmyk,

    /// <summary>
    /// Three red, green and blue components without a YCbCr transform.
    /// </summary>
    RGB,

    /// <summary>
    /// Three Y, Cb and Cr components. All chroma sampling ratios use this same conversion model.
    /// </summary>
    YCbCr
}
