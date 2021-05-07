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
        /// 8 bits per pixel, grayscale or color palette images.
        /// </summary>
        Bit8 = 8,

        /// <summary>
        /// 24 bits per pixel. One byte for each color channel.
        /// </summary>
        Bit24 = 24,
    }
}
