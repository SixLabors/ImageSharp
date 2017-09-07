﻿// <copyright file="Lanczos5Resampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing
{
    /// <summary>
    /// The function implements the Lanczos kernel algorithm as described on
    /// See <a href="https://en.wikipedia.org/wiki/Lanczos_resampling#Algorithm">this link</a> at Wikipedia for more information.
    /// with a radius of 5 pixels.
    /// </summary>
    public class Lanczos5Resampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 5;

        /// <inheritdoc/>
        public float GetValue(float x)
        {
            if (x < 0F)
            {
                x = -x;
            }

            if (x < 5F)
            {
                return MathF.SinC(x) * MathF.SinC(x / 5F);
            }

            return 0F;
        }
    }
}
