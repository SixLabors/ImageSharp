// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
{
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
        /// 6 bits per pixel. 2 bit for each color channel.
        ///
        /// Note: The TiffEncoder does not yet support 2 bits per color channel and will default to 24 bits per pixel instead.
        /// </summary>
        Bit6 = 6,

        /// <summary>
        /// 8 bits per pixel, grayscale or color palette images.
        /// </summary>
        Bit8 = 8,

        /// <summary>
        /// 12 bits per pixel. 4 bit for each color channel.
        ///
        /// Note: The TiffEncoder does not yet support 4 bits per color channel and will default to 24 bits per pixel instead.
        /// </summary>
        Bit12 = 12,

        /// <summary>
        /// 24 bits per pixel. One byte for each color channel.
        /// </summary>
        Bit24 = 24,

        /// <summary>
        /// 30 bits per pixel. 10 bit for each color channel.
        ///
        /// Note: The TiffEncoder does not yet support 10 bits per color channel and will default to 24 bits per pixel instead.
        /// </summary>
        Bit30 = 30,

        /// <summary>
        /// 42 bits per pixel. 14 bit for each color channel.
        ///
        /// Note: The TiffEncoder does not yet support 14 bits per color channel and will default to 24 bits per pixel instead.
        /// </summary>
        Bit42 = 42,
    }
}
