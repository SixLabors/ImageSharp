// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Provides enumeration of available PNG filter methods.
    /// </summary>
    public enum PngFilterMethod
    {
        /// <summary>
        /// With the None filter, the scanline is transmitted unmodified.
        /// </summary>
        None,

        /// <summary>
        /// The Sub filter transmits the difference between each byte and the value of the corresponding
        /// byte of the prior pixel.
        /// </summary>
        Sub,

        /// <summary>
        /// The Up filter is just like the <see cref="Sub"/> filter except that the pixel immediately above the current pixel,
        /// rather than just to its left, is used as the predictor.
        /// </summary>
        Up,

        /// <summary>
        /// The Average filter uses the average of the two neighboring pixels (left and above) to predict the value of a pixel.
        /// </summary>
        Average,

        /// <summary>
        /// The Paeth filter computes a simple linear function of the three neighboring pixels (left, above, upper left),
        /// then chooses as predictor the neighboring pixel closest to the computed value.
        /// </summary>
        Paeth,

        /// <summary>
        /// Computes the output scanline using all five filters, and selects the filter that gives the smallest sum of
        /// absolute values of outputs.
        /// This method usually outperforms any single fixed filter choice.
        /// </summary>
        Adaptive,
    }
}