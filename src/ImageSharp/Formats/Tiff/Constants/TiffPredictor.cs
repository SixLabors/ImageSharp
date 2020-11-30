// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff.Constants
{
    /// <summary>
    /// A mathematical operator that is applied to the image data before an encoding scheme is applied.
    /// </summary>
    public enum TiffPredictor : ushort
    {
        /// <summary>
        /// No prediction scheme used before coding
        /// </summary>
        None = 1,

        /// <summary>
        /// Horizontal differencing.
        /// </summary>
        Horizontal = 2,

        /// <summary>
        /// Floating point horizontal differencing.
        /// </summary>
        FloatingPoint = 3
    }
}
