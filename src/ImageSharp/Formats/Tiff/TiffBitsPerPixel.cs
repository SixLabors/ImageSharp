// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
        /// 10 bits per pixel, for gray images.
        ///
        /// Note: The TiffEncoder does not yet support 10 bits per pixel and will default to 24 bits per pixel instead.
        /// </summary>
        Bit10 = 10,

        /// <summary>
        /// 12 bits per pixel. 4 bit for each color channel.
        ///
        /// Note: The TiffEncoder does not yet support 4 bits per color channel and will default to 24 bits per pixel instead.
        /// </summary>
        Bit12 = 12,

        /// <summary>
        /// 14 bits per pixel, for gray images.
        ///
        /// Note: The TiffEncoder does not yet support 14 bits per pixel images and will default to 24 bits per pixel instead.
        /// </summary>
        Bit14 = 14,

        /// <summary>
        /// 16 bits per pixel, for gray images.
        ///
        /// Note: The TiffEncoder does not yet support 16 bits per color channel and will default to 24 bits per pixel instead.
        /// </summary>
        Bit16 = 16,

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
        /// 36 bits per pixel. 12 bit for each color channel.
        ///
        /// Note: The TiffEncoder does not yet support 12 bits per color channel and will default to 24 bits per pixel instead.
        /// </summary>
        Bit36 = 36,

        /// <summary>
        /// 42 bits per pixel. 14 bit for each color channel.
        ///
        /// Note: The TiffEncoder does not yet support 14 bits per color channel and will default to 24 bits per pixel instead.
        /// </summary>
        Bit42 = 42,

        /// <summary>
        /// 48 bits per pixel. 16 bit for each color channel.
        ///
        /// Note: The TiffEncoder does not yet support 16 bits per color channel and will default to 24 bits per pixel instead.
        /// </summary>
        Bit48 = 48,
    }
}
