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
        /// The Bits per samples is not known.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// One bit per sample for bicolor images.
        /// </summary>
        Bit1 = 1,

        /// <summary>
        /// Four bits per sample for grayscale images with 16 different levels of gray or paletted images with a palette of 16 colors.
        /// </summary>
        Bit4 = 4,

        /// <summary>
        /// Eight bits per sample for grayscale images with 256 different levels of gray or paletted images with a palette of 256 colors.
        /// </summary>
        Bit8 = 8,

        /// <summary>
        /// Six bits per sample, each channel has 2 bits.
        /// </summary>
        Bit6 = 6,

        /// <summary>
        /// Twelve bits per sample, each channel has 4 bits.
        /// </summary>
        Bit12 = 12,

        /// <summary>
        /// 24 bits per sample, each color channel has 8 Bits.
        /// </summary>
        Bit24 = 24,

        /// <summary>
        /// Thirty bits per sample, each channel has 10 bits.
        /// </summary>
        Bit30 = 30,

        /// <summary>
        /// Forty two bits per sample, each channel has 14 bits.
        /// </summary>
        Bit42 = 42,
    }
}
