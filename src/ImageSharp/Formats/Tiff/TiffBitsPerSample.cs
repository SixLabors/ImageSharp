// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// The number of bits per component.
    /// </summary>
    public enum TiffBitsPerSample
    {
        /// <summary>
        /// The bits per samples is not known.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// One bit per sample for bicolor images.
        /// </summary>
        Bit1,

        /// <summary>
        /// Two bits per sample for grayscale images with 4 different levels of gray or paletted images with a palette of 4 colors.
        /// </summary>
        Bit2,

        /// <summary>
        /// Four bits per sample for grayscale images with 16 different levels of gray or paletted images with a palette of 16 colors.
        /// </summary>
        Bit4,

        /// <summary>
        /// Six bits per sample for grayscale images.
        /// </summary>
        Bit6,

        /// <summary>
        /// Eight bits per sample for grayscale images with 256 different levels of gray or paletted images with a palette of 256 colors.
        /// </summary>
        Bit8,

        /// <summary>
        /// Ten bits per sample for grayscale images.
        /// </summary>
        Bit10,

        /// <summary>
        /// Twelve bits per sample for grayscale images.
        /// </summary>
        Bit12,

        /// <summary>
        /// Fourteen bits per sample for grayscale images.
        /// </summary>
        Bit14,

        /// <summary>
        /// Sixteen bits per sample for grayscale images.
        /// </summary>
        Bit16,

        /// <summary>
        /// 6 bits per sample, each channel has 2 bits.
        /// </summary>
        Rgb222,

        /// <summary>
        /// Twelve bits per sample, each channel has 4 bits.
        /// </summary>
        Rgb444,

        /// <summary>
        /// 24 bits per sample, each color channel has 8 Bits.
        /// </summary>
        Rgb888,

        /// <summary>
        /// Thirty bits per sample, each channel has 10 bits.
        /// </summary>
        Rgb101010,

        /// <summary>
        /// Thirty six bits per sample, each channel has 12 bits.
        /// </summary>
        Rgb121212,

        /// <summary>
        /// Forty two bits per sample, each channel has 14 bits.
        /// </summary>
        Rgb141414,

        /// <summary>
        /// Forty eight bits per sample, each channel has 16 bits.
        /// </summary>
        Rgb161616,
    }
}
