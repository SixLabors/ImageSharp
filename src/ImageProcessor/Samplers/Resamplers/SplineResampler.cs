// <copyright file="SplineResampler.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    /// <summary>
    /// The function implements the spline algorithm.
    /// <see href="http://www.imagemagick.org/Usage/filter/#cubic_bc"/>
    /// </summary>
    public class SplineResampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 2;

        /// <inheritdoc/>
        public float GetValue(float x)
        {
            const float B = 1;
            const float C = 0;

            return ImageMaths.GetBcValue(x, B, C);
        }
    }
}
