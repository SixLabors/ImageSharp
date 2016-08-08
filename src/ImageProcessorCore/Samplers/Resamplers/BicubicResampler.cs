// <copyright file="BicubicResampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// The function implements the bicubic kernel algorithm W(x) as described on
    /// <see href="https://en.wikipedia.org/wiki/Bicubic_interpolation#Bicubic_convolution_algorithm">Wikipedia</see>
    /// A commonly used algorithm within imageprocessing that preserves sharpness better than triangle interpolation.
    /// </summary>
    public class BicubicResampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 2;

        /// <inheritdoc/>
        public float GetValue(float x)
        {
            if (x < 0F)
            {
                x = -x;
            }

            float result = 0;

            // Given the coefficient "a" as -0.5F.
            if (x <= 1F)
            {
                // Below simplified result = ((a + 2F) * (x * x * x)) - ((a + 3F) * (x * x)) + 1;
                result = (((1.5F * x) - 2.5F) * x * x) + 1;
            }
            else if (x < 2F)
            {
                // Below simplified result = (a * (x * x * x)) - ((5F * a) * (x * x)) + ((8F * a) * x) - (4F * a);
                result = (((((-0.5F * x) + 2.5F) * x) - 4) * x) + 2;
            }

            return result;
        }
    }
}
