// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// The Hermite filter is type of smoothed triangular interpolation Filter,
    /// This filter rounds off strong edges while preserving flat 'color levels' in the original image.
    /// <see href="http://www.imagemagick.org/Usage/filter/#cubic_bc"/>
    /// </summary>
    public class HermiteResampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 2;

        /// <inheritdoc/>
        public float GetValue(float x)
        {
            const float B = 0F;
            const float C = 0F;

            return ImageMaths.GetBcValue(x, B, C);
        }
    }
}