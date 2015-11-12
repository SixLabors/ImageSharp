// <copyright file="BoxResampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    /// <summary>
    /// The function implements the box (nearest neighbour) algorithm.
    /// </summary>
    public class BoxResampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 0.5f;

        /// <inheritdoc/>
        public float GetValue(float x)
        {
            if (x < 0)
            {
                x = -x;
            }

            if (x <= 0.5)
            {
                return 1;
            }

            return 0;
        }
    }
}
