// <copyright file="BoxResampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// The function implements the box algorithm. Similar to nearest neighbour when upscaling.
    /// When downscaling the pixels will average, merging together.
    /// </summary>
    public class BoxResampler : IResampler
    {
        /// <inheritdoc/>
        public double Radius => 0.5d;

        /// <inheritdoc/>
        public double GetValue(double x)
        {
            if (x > -0.5 && x <= 0.5)
            {
                return 1;
            }

            return 0;
        }
    }
}
