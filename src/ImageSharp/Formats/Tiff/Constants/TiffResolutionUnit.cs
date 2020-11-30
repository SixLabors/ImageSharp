// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff.Constants
{
    /// <summary>
    /// Enumeration representing the resolution units defined by the Tiff file-format.
    /// </summary>
    public enum TiffResolutionUnit : ushort
    {
        /// <summary>
        /// No absolute unit of measurement.
        /// </summary>
        None = 1,

        /// <summary>
        /// Inch.
        /// </summary>
        Inch = 2,

        /// <summary>
        /// Centimeter.
        /// </summary>
        Centimeter = 3
    }
}
