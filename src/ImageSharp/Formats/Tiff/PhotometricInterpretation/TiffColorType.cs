// <copyright file="TiffColorType.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Tiff
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
    }
}
