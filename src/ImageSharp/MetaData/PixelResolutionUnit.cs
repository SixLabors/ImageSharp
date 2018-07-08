// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.MetaData
{
    /// <summary>
    /// Provides enumeration of available pixel density units.
    /// </summary>
    public enum PixelResolutionUnit : byte
    {
        /// <summary>
        /// No units; width:height pixel aspect ratio.
        /// </summary>
        AspectRatio = 0,

        /// <summary>
        /// Pixels per inch (2.54 cm).
        /// </summary>
        PixelsPerInch = 1,

        /// <summary>
        /// Pixels per centimeter.
        /// </summary>
        PixelsPerCentimeter = 2,

        /// <summary>
        /// Pixels per meter (100 cm).
        /// </summary>
        PixelsPerMeter = 3
    }
}
