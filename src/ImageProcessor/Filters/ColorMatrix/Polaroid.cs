// <copyright file="Polaroid.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating an old Polaroid effect.
    /// </summary>
    public class Polaroid : ColorMatrixFilter
    {
        /// <summary>
        /// The Polaroid matrix. Purely artistic in composition.
        /// TODO: Calculate a matrix that works in the linear color space.
        /// </summary>
        private static readonly ColorMatrix Matrix = new ColorMatrix(
            new[]
                {
                    new[] { 1.638f, -0.062f, -0.262f, 0, 0 },
                    new[] { -0.122f, 1.378f, -0.122f, 0, 0 },
                    new[] { 1.016f, -0.016f, 1.383f, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new[] { 0.06f, -0.05f, -0.05f, 0, 1 }
                });

        /// <summary>
        /// Initializes a new instance of the <see cref="Polaroid"/> class.
        /// </summary>
        public Polaroid()
            : base(Matrix, false)
        {
        }
    }
}
