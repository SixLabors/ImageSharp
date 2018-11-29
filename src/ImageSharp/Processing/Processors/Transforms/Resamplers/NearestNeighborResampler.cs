// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// The function implements the nearest neighbor algorithm. This uses an unscaled filter
    /// which will select the closest pixel to the new pixels position.
    /// </summary>
    public class NearestNeighborResampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 1;

        /// <inheritdoc/>
        public float GetValue(float x)
        {
            return x;
        }
    }
}