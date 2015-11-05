// <copyright file="Saturation.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System;
    using System.Numerics;

    /// <summary>
    /// An <see cref="IImageProcessor"/> to change the saturation of an <see cref="Image"/>.
    /// </summary>
    public class Saturation : ColorMatrixFilter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Saturation"/> class.
        /// </summary>
        /// <param name="saturation">The new saturation of the image. Must be between -100 and 100.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="saturation"/> is less than -100 or is greater than 100.
        /// </exception>
        public Saturation(int saturation)
        {
            Guard.MustBeBetweenOrEqualTo(saturation, -100, 100, nameof(saturation));
            float saturationFactor = saturation / 100f;

            // Stop at -1 to prevent inversion.
            saturationFactor++;

            // The matrix is set up to "shear" the colour space using the following set of values.
            // Note that each colour component has an effective luminance which contributes to the
            // overall brightness of the pixel.
            // See http://graficaobscura.com/matrix/index.html
            float saturationComplement = 1.0f - saturationFactor;
            float saturationComplementR = 0.3086f * saturationComplement;
            float saturationComplementG = 0.6094f * saturationComplement;
            float saturationComplementB = 0.0820f * saturationComplement;

            Matrix4x4 matrix = new Matrix4x4()
            {
                M11 = saturationComplementR + saturationFactor,
                M12 = saturationComplementR,
                M13 = saturationComplementR,
                M21 = saturationComplementG,
                M22 = saturationComplementG + saturationFactor,
                M23 = saturationComplementG,
                M31 = saturationComplementB,
                M32 = saturationComplementB,
                M33 = saturationComplementB + saturationFactor,
            };

            this.Matrix = matrix;
        }
    }
}
