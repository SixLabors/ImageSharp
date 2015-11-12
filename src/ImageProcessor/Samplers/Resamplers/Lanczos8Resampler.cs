// <copyright file="Lanczos8Resampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    /// <summary>
    /// The function implements the Lanczos kernel algorithm as described on
    /// <see href="https://en.wikipedia.org/wiki/Lanczos_resampling#Algorithm">Wikipedia</see>
    /// </summary>
    public class Lanczos8Resampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 8;

        /// <inheritdoc/>
        public float GetValue(float x)
        {
            if (x < 0)
            {
                x = -x;
            }

            if (x < 8)
            {
                return ImageMaths.SinC(x) * ImageMaths.SinC(x / 8f);
            }

            return 0;
        }
    }
}
