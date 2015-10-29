// <copyright file="BicubicResampler.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    /// <summary>
    /// The function implements the bicubic kernel algorithm W(x) as described on
    /// <see href="https://en.wikipedia.org/wiki/Bicubic_interpolation#Bicubic_convolution_algorithm">Wikipedia</see>
    /// </summary>
    public class BicubicResampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 2;

        /// <inheritdoc/>
        public float GetValue(float x)
        {
            // The coefficient.
            float a = -0.5f;

            if (x < 0)
            {
                x = -x;
            }

            float result = 0;

            if (x <= 1)
            {
                result = (((1.5f * x) - 2.5f) * x * x) + 1;
            }
            else if (x < 2)
            {
                result = (((((a * x) + 2.5f) * x) - 4) * x) + 2;
            }

            return result;
        }
    }
}
