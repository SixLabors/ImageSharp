// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// Provides enumeration of available pixel density units.
    /// </summary>
    public enum DensityUnits : byte
    {
        /// <summary>
        /// No units; width:height pixel aspect ratio = Ydensity:Xdensity
        /// </summary>
        AspectRatio = 0,

        /// <summary>
        /// Pixels per inch (2.54 cm)
        /// </summary>
        PixelsPerInch = 1, // Other image formats would default to this.

        /// <summary>
        /// Pixels per centimeter.
        /// </summary>
        PixelsPerCentimeter = 2
    }
}
