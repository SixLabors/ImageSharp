// <copyright file="RobidouxResampler.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    /// <summary>
    /// The function implements the Robidoux algorithm.
    /// <see href="http://www.imagemagick.org/Usage/filter/#robidoux"/>
    /// </summary>
    public class RobidouxResampler : IResampler
    {
        /// <inheritdoc/>
        public double Radius => 2;

        /// <inheritdoc/>
        public double GetValue(double x)
        {
            const double B = 0.3782;
            const double C = 0.3109;

            return ImageMaths.GetBcValue(x, B, C);
        }
    }
}
