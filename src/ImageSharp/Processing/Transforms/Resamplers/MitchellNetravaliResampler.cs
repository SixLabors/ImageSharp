// <copyright file="MitchellNetravaliResampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing
{
    /// <summary>
    /// The function implements the mitchell algorithm as described on
    /// See <a href="https://de.wikipedia.org/wiki/Mitchell-Netravali-Filter">this link</a> at Wikipedia for more information.
    /// </summary>
    public class MitchellNetravaliResampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 2;

        /// <inheritdoc/>
        public float GetValue(float x)
        {
            const float B = 0.3333333F;
            const float C = 0.3333333F;

            return ImageMaths.GetBcValue(x, B, C);
        }
    }
}
