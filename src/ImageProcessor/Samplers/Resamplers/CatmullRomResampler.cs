// <copyright file="CatmullRomResampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    /// <summary>
    /// The function implements the Catmull-Rom algorithm.
    /// <see href="http://www.imagemagick.org/Usage/filter/#cubic_bc"/>
    /// </summary>
    public class CatmullRomResampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 2;

        /// <inheritdoc/>
        public float GetValue(float x)
        {
            const float B = 0;
            const float C = 1 / 2f;

            return ImageMaths.GetBcValue(x, B, C);
        }
    }
}
