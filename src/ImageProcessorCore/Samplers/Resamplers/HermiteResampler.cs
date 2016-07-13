// <copyright file="HermiteResampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// The Hermite filter is type of smoothed triangular interpolation Filter, 
    /// This filter rounds off strong edges while preserving flat 'color levels' in the original image.
    /// <see href="http://www.imagemagick.org/Usage/filter/#cubic_bc"/>
    /// </summary>
    public class HermiteResampler : IResampler
    {
        /// <inheritdoc/>
        public double Radius => 2;

        /// <inheritdoc/>
        public double GetValue(double x)
        {
            const double B = 0;
            const double C = 0;

            return ImageMaths.GetBcValue(x, B, C);
        }
    }
}
