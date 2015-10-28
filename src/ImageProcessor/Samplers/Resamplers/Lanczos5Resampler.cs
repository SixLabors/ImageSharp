// <copyright file="Lanczos5Resampler.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    /// <summary>
    /// The function implements the Lanczos kernel algorithm as described on
    /// <see href="https://en.wikipedia.org/wiki/Lanczos_resampling#Algorithm">Wikipedia</see>
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
                return ImageMaths.SinC(x) * ImageMaths.SinC(x / 5f);
            }

            return 0;
        }
    }
}
