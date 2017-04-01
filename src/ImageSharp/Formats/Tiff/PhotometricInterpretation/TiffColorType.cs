// <copyright file="TiffColorType.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Provides enumeration of the various TIFF photometric interpretation implementation types.
    /// </summary>
    internal enum TiffColorType
    {
        /// <summary>
        /// Grayscale:  0 is imaged as white. The maximum value is imaged as black. Optimised implementation for 8-bit images.
        /// </summary>
        WhiteIsZero8
    }
}
