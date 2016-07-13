// <copyright file="RobidouxResampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
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
            const double B = 0.37821575509399867D;
            const double C = 0.31089212245300067D;

            return ImageMaths.GetBcValue(x, B, C);
        }
    }
}
