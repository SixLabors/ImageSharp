// <copyright file="RobidouxSharpResampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// The function implements the Robidoux Sharp algorithm.
    /// <see href="http://www.imagemagick.org/Usage/filter/#robidoux"/>
    /// </summary>
    public class RobidouxSharpResampler : IResampler
    {
        /// <inheritdoc/>
        public double Radius => 2;

        /// <inheritdoc/>
        public double GetValue(double x)
        {
            const double B = 0.2620145123990142D;
            const double C = 0.3689927438004929D;

            return ImageMaths.GetBcValue(x, B, C);
        }
    }
}
