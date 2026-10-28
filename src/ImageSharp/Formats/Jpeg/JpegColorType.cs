// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg;

/// <summary>
/// Describes JPEG output components and YCbCr chroma sampling.
/// </summary>
/// <remarks>
/// YCbCr follows ITU-T T.871. The YCbCr values specify both the color model and the sampling ratio.
/// CMYK and YCCK encoding use the Adobe JPEG sample conventions.
/// </remarks>
public enum JpegColorType : byte
{
    /// <summary>
    /// YCbCr with Cb and Cr sampled at half the horizontal and vertical resolution of Y.
    /// </summary>
    YCbCrRatio420 = 0,

    /// <summary>
    /// YCbCr with Y, Cb and Cr sampled at the same resolution, without chroma subsampling.
    /// </summary>
    YCbCrRatio444 = 1,

    /// <summary>
    /// YCbCr with Cb and Cr sampled at half the horizontal resolution of Y and the same vertical resolution.
    /// </summary>
    YCbCrRatio422 = 2,

    /// <summary>
    /// YCbCr with Cb and Cr sampled at one-quarter the horizontal resolution of Y and the same vertical resolution.
    /// </summary>
    YCbCrRatio411 = 3,

    /// <summary>
    /// YCbCr with Cb and Cr sampled at one-quarter the horizontal resolution of Y and half the vertical resolution.
    /// </summary>
    YCbCrRatio410 = 4,

    /// <summary>
    /// One luminance component.
    /// </summary>
    Luminance = 5,

    /// <summary>
    /// Three red, green and blue components without chroma subsampling.
    /// </summary>
    Rgb = 6,

    /// <summary>
    /// Four cyan, magenta, yellow and black components using the Adobe JPEG convention.
    /// </summary>
    /// <remarks>
    /// Stored ink values are inverted: zero means full ink and the maximum sample value means no ink.
    /// </remarks>
    Cmyk = 7,

    /// <summary>
    /// Four Y, Cb, Cr and black components using the Adobe JPEG convention.
    /// </summary>
    /// <remarks>
    /// The stored black component is inverted: zero means full black ink and the maximum sample value means no black ink.
    /// </remarks>
    Ycck = 8,
}
