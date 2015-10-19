// <copyright file="TriangleResampler.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    /// <summary>
    /// The function implements the triangle (bilinear) algorithm.
    /// </summary>
    public class TriangleResampler : IResampler
    {
        /// <inheritdoc/>
        public double Radius => 1;

        /// <inheritdoc/>
        public double GetValue(double x)
        {
            if (x < 0)
            {
                x = -x;
            }

            if (x < 1)
            {
                return 1 - x;
            }

            return 0;
        }
    }
}
