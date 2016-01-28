// <copyright file="WelchResampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Samplers
{
    /// <summary>
    /// The function implements the welch algorithm.
    /// <see href="http://www.imagemagick.org/Usage/filter/"/>
    /// </summary>
    public class WelchResampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 3;

        /// <inheritdoc/>
        public float GetValue(float x)
        {
            if (x < 0)
            {
                x = -x;
            }

            if (x < 3)
            {
                return ImageMaths.SinC(x) * (1.0f - (x * x / 9.0f));
            }

            return 0;
        }
    }
}
