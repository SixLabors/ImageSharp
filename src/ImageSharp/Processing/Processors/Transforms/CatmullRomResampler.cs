// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// The Catmull-Rom filter is a well known standard Cubic Filter often used as a interpolation function.
    /// This filter produces a reasonably sharp edge, but without a the pronounced gradient change on large
    /// scale image enlargements that a 'Lagrange' filter can produce.
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
            const float C = 0.5F;

            return ImageMaths.GetBcValue(x, B, C);
        }
    }
}