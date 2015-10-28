// <copyright file="BoxResampler.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    /// <summary>
    /// The function implements the box (nearest neighbour) algorithm.
    /// </summary>
    public class BoxResampler : IResampler
    {
        /// <inheritdoc/>
        public double Radius => 0.5;

        /// <inheritdoc/>
        public double GetValue(double x)
        {
            if (x < 0)
            {
                x = -x;
            }

            if (x <= 0.5)
            {
                return 1;
            }

            return 0;
        }
    }
}
