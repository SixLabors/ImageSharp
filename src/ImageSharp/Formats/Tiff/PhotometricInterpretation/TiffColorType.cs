// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Provides enumeration of the various TIFF photometric interpretation implementation types.
    /// </summary>
    internal enum TiffColorType
    {
        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white.
        /// </summary>
        BlackIsZero,

        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white. Optimized implementation for bilevel images.
        /// </summary>
        BlackIsZero1,

        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white. Optimized implementation for 4-bit images.
        /// </summary>
        BlackIsZero4,

        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white. Optimized implementation for 8-bit images.
        /// </summary>
        BlackIsZero8,

        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white. Optimized implementation for 16-bit images.
        /// </summary>
        BlackIsZero16,

        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white. Optimized implementation for 24-bit images.
        /// </summary>
        BlackIsZero24,

        /// <summary>
        /// Grayscale: 0 is imaged as black. The maximum value is imaged as white. Optimized implementation for 32-bit images.
        /// </summary>
        BlackIsZero32,

        /// <summary>
        /// Grayscale: 0 is imaged as white. The maximum value is imaged as black.
        /// </summary>
        WhiteIsZero,

        /// <summary>
        /// Grayscale: 0 is imaged as white. The maximum value is imaged as black. Optimized implementation for bilevel images.
        /// </summary>
        WhiteIsZero1,

        /// <summary>
        /// Grayscale: 0 is imaged as white. The maximum value is imaged as black. Optimized implementation for 4-bit images.
        /// </summary>
        WhiteIsZero4,

        /// <summary>
        /// Grayscale: 0 is imaged as white. The maximum value is imaged as black. Optimized implementation for 8-bit images.
        /// </summary>
        WhiteIsZero8,

        /// <summary>
        /// Grayscale: 0 is imaged as white. The maximum value is imaged as black. Optimized implementation for 16-bit images.
        /// </summary>
        WhiteIsZero16,

        /// <summary>
        /// Grayscale: 0 is imaged as white. The maximum value is imaged as black. Optimized implementation for 24-bit images.
        /// </summary>
        WhiteIsZero24,

        /// <summary>
        /// Grayscale: 0 is imaged as white. The maximum value is imaged as black. Optimized implementation for 32-bit images.
        /// </summary>
        WhiteIsZero32,

        /// <summary>
        /// Palette-color.
        /// </summary>
        PaletteColor,

        /// <summary>
        /// RGB Full Color.
        /// </summary>
        Rgb,

        /// <summary>
        /// RGB color image with 2 bits for each channel.
        /// </summary>
        Rgb222,

        /// <summary>
        /// RGB color image with 4 bits for each channel.
        /// </summary>
        Rgb444,

        /// <summary>
        /// RGB Full Color. Optimized implementation for 8-bit images.
        /// </summary>
        Rgb888,

        /// <summary>
        /// RGB color image with 10 bits for each channel.
        /// </summary>
        Rgb101010,

        /// <summary>
        /// RGB color image with 12 bits for each channel.
        /// </summary>
        Rgb121212,

        /// <summary>
        /// RGB color image with 14 bits for each channel.
        /// </summary>
        Rgb141414,

        /// <summary>
        /// RGB color image with 16 bits for each channel.
        /// </summary>
        Rgb161616,

        /// <summary>
        /// RGB color image with 24 bits for each channel.
        /// </summary>
        Rgb242424,

        /// <summary>
        /// RGB color image with 32 bits for each channel.
        /// </summary>
        Rgb323232,

        /// <summary>
        /// RGB Full Color. Planar configuration of data. 8 Bit per color channel.
        /// </summary>
        Rgb888Planar,

        /// <summary>
        /// RGB Full Color. Planar configuration of data. 16 Bit per color channel.
        /// </summary>
        Rgb161616Planar,

        /// <summary>
        /// RGB Full Color. Planar configuration of data. 24 Bit per color channel.
        /// </summary>
        Rgb242424Planar,

        /// <summary>
        /// RGB Full Color. Planar configuration of data. 32 Bit per color channel.
        /// </summary>
        Rgb323232Planar,
    }
}
