// <copyright file="WelchResampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// The function implements the welch algorithm.
    /// <see href="http://www.imagemagick.org/Usage/filter/"/>
    /// </summary>
    public class WelchResampler : IResampler
    {
        /// <inheritdoc/>
        public double Radius => 3;

        /// <inheritdoc/>
        public double GetValue(double x)
        {
            if (x < 0)
            {
                x = -x;
            }

            if (x < 3)
            {
                return ImageMaths.SinC(x) * (1.0d - (x * x / 9.0d));
            }

            return 0;
        }
    }
}
