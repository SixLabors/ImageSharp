// <copyright file="Lanczos5Resampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// The function implements the Lanczos kernel algorithm as described on
    /// <see href="https://en.wikipedia.org/wiki/Lanczos_resampling#Algorithm">Wikipedia</see>
    /// with a radius of 5 pixels.
    /// </summary>
    public class Lanczos5Resampler : IResampler
    {
        /// <inheritdoc/>
        public double Radius => 5;

        /// <inheritdoc/>
        public double GetValue(double x)
        {
            if (x < 0)
            {
                x = -x;
            }

            if (x < 5)
            {
                return ImageMaths.SinC(x) * ImageMaths.SinC(x / 5d);
            }

            return 0;
        }
    }
}
