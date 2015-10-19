// <copyright file="RobidouxSoftResampler.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    /// <summary>
    /// The function implements the Robidoux Soft algorithm.
    /// <see href="http://www.imagemagick.org/Usage/filter/#robidoux"/>
    /// </summary>
    public class RobidouxSoftResampler : IResampler
    {
        /// <inheritdoc/>
        public double Radius => 2;

        /// <inheritdoc/>
        public double GetValue(double x)
        {
            const double B = 0.6796;
            const double C = 0.1602;

            return ImageMaths.GetBcValue(x, B, C);
        }
    }
}
