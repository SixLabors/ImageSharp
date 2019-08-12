// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Provides enumeration of the various TIFF photometric interpretation implementation types.
    /// </summary>
    internal enum TiffColorType
    {
        /// <summary>
        /// Grayscale:  0 is imaged as black. The maximum value is imaged as white.
        /// </summary>
        BlackIsZero,

        /// <summary>
        /// Grayscale:  0 is imaged as black. The maximum value is imaged as white. Optimised implementation for bilevel images.
        /// </summary>
        BlackIsZero1,

        /// <summary>
        /// Grayscale:  0 is imaged as black. The maximum value is imaged as white. Optimised implementation for 4-bit images.
        /// </summary>
        BlackIsZero4,

        /// <summary>
        /// Grayscale:  0 is imaged as black. The maximum value is imaged as white. Optimised implementation for 8-bit images.
        /// </summary>
        BlackIsZero8,

        /// <summary>
        /// Grayscale:  0 is imaged as white. The maximum value is imaged as black.
        /// </summary>
        WhiteIsZero,

        /// <summary>
        /// Grayscale:  0 is imaged as white. The maximum value is imaged as black. Optimised implementation for bilevel images.
        /// </summary>
        WhiteIsZero1,

        /// <summary>
        /// Grayscale:  0 is imaged as white. The maximum value is imaged as black. Optimised implementation for 4-bit images.
        /// </summary>
        WhiteIsZero4,

        /// <summary>
        /// Grayscale:  0 is imaged as white. The maximum value is imaged as black. Optimised implementation for 8-bit images.
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
        /// RGB Full Color. Optimised implementation for 8-bit images.
        /// </summary>
        Rgb888,

        /// <summary>
        /// RGB Full Color. Planar configuration of data.
        /// </summary>
        RgbPlanar,
    }
}
