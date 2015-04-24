// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PixelOperations.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Performs per-pixel operations.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Helpers
{
    using System;
    using System.Drawing;
    using ImageProcessor.Common.Extensions;

    /// <summary>
    /// Performs per-pixel operations.
    /// </summary>
    public static class PixelOperations
    {
        /// <summary>
        /// Returns the given color adjusted by the given gamma value.
        /// </summary>
        /// <param name="color">
        /// The <see cref="Color"/> to adjust.
        /// </param>
        /// <param name="value">
        /// The gamma value - Between .1 and 5.
        /// </param>
        /// <returns>
        /// The adjusted <see cref="Color"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the given gamma value is out with the acceptable range.
        /// </exception>
        public static Color Gamma(Color color, float value)
        {
            if (value > 5 || value < .1)
            {
                throw new ArgumentOutOfRangeException("value", "Value should be between .1 and 5.");
            }

            byte[] ramp = new byte[256];
            for (int x = 0; x < 256; ++x)
            {
                byte val = ((255.0 * Math.Pow(x / 255.0, value)) + 0.5).ToByte();
                ramp[x] = val;
            }

            byte r = ramp[color.R];
            byte g = ramp[color.G];
            byte b = ramp[color.B];

            return Color.FromArgb(color.A, r, g, b);
        }
    }
}
