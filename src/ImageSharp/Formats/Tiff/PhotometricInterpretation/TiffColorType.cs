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
        /// RGB color image with 14 bits for each channel.
        /// </summary>
        Rgb141414,

        /// <summary>
        /// RGB Full Color. Planar configuration of data.
        /// </summary>
        RgbPlanar,
    }
}
