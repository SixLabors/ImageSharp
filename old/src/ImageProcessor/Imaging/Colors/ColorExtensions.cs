// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ColorExtensions.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides extensions for manipulating colors.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Colors
{
    using System;
    using System.Drawing;

    using ImageProcessor.Common.Extensions;

    /// <summary>
    /// Provides extensions for manipulating colors.
    /// </summary>
    internal static class ColorExtensions
    {
        /// <summary>
        /// Adds colors together using the RGBA color format.
        /// </summary>
        /// <param name="color">
        /// The color to add to.
        /// </param>
        /// <param name="colors">
        /// The colors to add to the initial one.
        /// </param>
        /// <returns>
        /// The combined <see cref="Color"/>.
        /// </returns>
        public static Color Add(this Color color, params Color[] colors)
        {
            int red = color.A > 0 ? color.R : 0;
            int green = color.A > 0 ? color.G : 0;
            int blue = color.A > 0 ? color.B : 0;
            int alpha = color.A;

            int counter = 0;
            foreach (Color addColor in colors)
            {
                if (addColor.A > 0)
                {
                    counter += 1;
                    red += addColor.R;
                    green += addColor.G;
                    blue += addColor.B;
                    alpha += addColor.A;
                }
            }

            counter = Math.Max(1, counter);

            return Color.FromArgb((alpha / counter).ToByte(), (red / counter).ToByte(), (green / counter).ToByte(), (blue / counter).ToByte());
        }

        /// <summary>
        /// Adds colors together using the CMYK color format.
        /// </summary>
        /// <param name="color">
        /// The color to add to.
        /// </param>
        /// <param name="colors">
        /// The colors to add to the initial one.
        /// </param>
        /// <returns>
        /// The combined <see cref="CmykColor"/>.
        /// </returns>
        public static CmykColor AddAsCmykColor(this Color color, params Color[] colors)
        {
            CmykColor cmyk = color;
            float c = color.A > 0 ? cmyk.C : 0;
            float m = color.A > 0 ? cmyk.M : 0;
            float y = color.A > 0 ? cmyk.Y : 0;
            float k = color.A > 0 ? cmyk.K : 0;

            foreach (Color addColor in colors)
            {
                if (addColor.A > 0)
                {
                    CmykColor cmykAdd = addColor;
                    c += cmykAdd.C;
                    m += cmykAdd.M;
                    y += cmykAdd.Y;
                    k += cmykAdd.K;
                }
            }

            return CmykColor.FromCmykColor(c, m, y, k);
        }
    }
}
