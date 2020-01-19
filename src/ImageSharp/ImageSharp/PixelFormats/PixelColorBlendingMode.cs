// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Enumerates the various color blending modes.
    /// </summary>
    public enum PixelColorBlendingMode
    {
        /// <summary>
        /// Default blending mode, also known as "Normal" or "Alpha Blending"
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Blends the 2 values by multiplication.
        /// </summary>
        Multiply,

        /// <summary>
        /// Blends the 2 values by addition.
        /// </summary>
        Add,

        /// <summary>
        /// Blends the 2 values by subtraction.
        /// </summary>
        Subtract,

        /// <summary>
        /// Multiplies the complements of the backdrop and source values, then complements the result.
        /// </summary>
        Screen,

        /// <summary>
        /// Selects the minimum of the backdrop and source values.
        /// </summary>
        Darken,

        /// <summary>
        /// Selects the max of the backdrop and source values.
        /// </summary>
        Lighten,

        /// <summary>
        /// Multiplies or screens the values, depending on the backdrop vector values.
        /// </summary>
        Overlay,

        /// <summary>
        /// Multiplies or screens the colors, depending on the source value.
        /// </summary>
        HardLight,
    }
}
