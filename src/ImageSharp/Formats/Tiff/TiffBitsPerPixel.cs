// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff;

/// <summary>
/// Enumerates the available bits per pixel for the tiff format.
/// </summary>
public enum TiffBitsPerPixel
{
    /// <summary>
    /// 1 bit per pixel, for bi-color image.
    /// </summary>
    Bit1 = 1,

    /// <summary>
    /// 4 bits per pixel, for images with a color palette.
    /// </summary>
    Bit4 = 4,

    /// <summary>
    /// <para>6 bits per pixel. 2 bit for each color channel.</para>
    /// <para>Note: The TiffEncoder does not yet support 2 bits per color channel and will default to 24 bits per pixel instead.</para>
    /// </summary>
    Bit6 = 6,

    /// <summary>
    /// 8 bits per pixel, grayscale or color palette images.
    /// </summary>
    Bit8 = 8,

    /// <summary>
    /// <para>10 bits per pixel, for gray images.</para>
    /// <para>Note: The TiffEncoder does not yet support 10 bits per pixel and will default to 24 bits per pixel instead.</para>
    /// </summary>
    Bit10 = 10,

    /// <summary>
    /// <para>12 bits per pixel. 4 bit for each color channel.</para>
    /// <para>Note: The TiffEncoder does not yet support 4 bits per color channel and will default to 24 bits per pixel instead.</para>
    /// </summary>
    Bit12 = 12,

    /// <summary>
    /// <para>14 bits per pixel, for gray images.</para>
    /// <para>Note: The TiffEncoder does not yet support 14 bits per pixel images and will default to 24 bits per pixel instead.</para>
    /// </summary>
    Bit14 = 14,

    /// <summary>
    /// <para>16 bits per pixel, for gray images.</para>
    /// <para>Note: The TiffEncoder does not yet support 16 bits per color channel and will default to 16 bits grayscale instead.</para>
    /// </summary>
    Bit16 = 16,

    /// <summary>
    /// 24 bits per pixel. One byte for each color channel.
    /// </summary>
    Bit24 = 24,

    /// <summary>
    /// <para>30 bits per pixel. 10 bit for each color channel.</para>
    /// <para>Note: The TiffEncoder does not yet support 10 bits per color channel and will default to 24 bits per pixel instead.</para>
    /// </summary>
    Bit30 = 30,

    /// <summary>
    /// 32 bits per pixel. One byte for each color channel.
    /// </summary>
    Bit32 = 32,

    /// <summary>
    /// <para>36 bits per pixel. 12 bit for each color channel.</para>
    /// <para>Note: The TiffEncoder does not yet support 12 bits per color channel and will default to 24 bits per pixel instead.</para>
    /// </summary>
    Bit36 = 36,

    /// <summary>
    /// <para>42 bits per pixel. 14 bit for each color channel.</para>
    /// <para>Note: The TiffEncoder does not yet support 14 bits per color channel and will default to 24 bits per pixel instead.</para>
    /// </summary>
    Bit42 = 42,

    /// <summary>
    /// <para>48 bits per pixel. 16 bit for each color channel.</para>
    /// <para>Note: The TiffEncoder does not yet support 16 bits per color channel and will default to 24 bits per pixel instead.</para>
    /// </summary>
    Bit48 = 48,

    /// <summary>
    /// <para>64 bits per pixel. 16 bit for each color channel.</para>
    /// <para>Note: The TiffEncoder does not yet support 16 bits per color channel and will default to 32 bits per pixel instead.</para>
    /// </summary>
    Bit64 = 64,
}
