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
        public double Radius => 2;

        /// <inheritdoc/>
        public double GetValue(double x)
        {
            // The coefficient.
            double a = -0.5d;

            if (x < 0)
            {
                x = -x;
            }

            double result = 0;

            if (x <= 1)
            {
                result = (((1.5d * x) - 2.5d) * x * x) + 1;
            }
            else if (x < 2)
            {
                result = (((((a * x) + 2.5d) * x) - 4) * x) + 2;
            }

            return result;
        }
    }
}
