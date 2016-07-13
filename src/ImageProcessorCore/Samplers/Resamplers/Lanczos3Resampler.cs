// <copyright file="Lanczos3Resampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// The function implements the Lanczos kernel algorithm as described on
    /// <see href="https://en.wikipedia.org/wiki/Lanczos_resampling#Algorithm">Wikipedia</see>
    /// with a radius of 3 pixels.
    /// </summary>
    public class Lanczos3Resampler : IResampler
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
                return ImageMaths.SinC(x) * ImageMaths.SinC(x / 3d);
            }

            return 0;
        }
    }
}
