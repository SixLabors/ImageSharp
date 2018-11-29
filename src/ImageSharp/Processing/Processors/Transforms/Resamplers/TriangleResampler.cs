// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// The function implements the triangle (bilinear) algorithm.
    /// Bilinear interpolation can be used where perfect image transformation with pixel matching is impossible,
    /// so that one can calculate and assign appropriate intensity values to pixels.
    /// </summary>
    public class TriangleResampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 1;

        /// <inheritdoc/>
        public float GetValue(float x)
        {
            if (x < 0F)
            {
                x = -x;
            }

            if (x < 1F)
            {
                return 1F - x;
            }

            return 0F;
        }
    }
}