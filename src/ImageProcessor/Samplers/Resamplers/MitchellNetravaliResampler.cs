// <copyright file="MitchellResampler.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    /// <summary>
    /// The function implements the mitchell algorithm as described on
    /// <see href="https://de.wikipedia.org/wiki/Mitchell-Netravali-Filter">Wikipedia</see>
    /// </summary>
    public class MitchellNetravaliResampler : IResampler
    {
        /// <inheritdoc/>
        public double Radius => 2;

        /// <inheritdoc/>
        public double GetValue(double x)
        {
            const double B = 1 / 3d;
            const double C = 1 / 3d;

            return ImageMaths.GetBcValue(x, B, C);
        }
    }
}
