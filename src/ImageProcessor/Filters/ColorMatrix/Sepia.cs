// <copyright file="Sepia.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    /// <summary>
    /// Converts the colors of the image to their sepia equivalent recreating an old photo effect.
    /// </summary>
    public class Sepia : ColorMatrixFilter
    {
        /// <summary>
        /// The sepia matrix.
        /// TODO: Calculate a matrix that works in the linear color space.
        /// </summary>
        private static readonly ColorMatrix Matrix = new ColorMatrix(
            new[]
                {
                    new[] { .393f, .349f, .272f, 0, 0 },
                    new[] { .769f, .686f, .534f, 0, 0 },
                    new[] { .189f, .168f, .131f, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 0, 0, 0, 0, 1 }
                });

        /// <summary>
        /// Initializes a new instance of the <see cref="Sepia"/> class.
        /// </summary>
        public Sepia()
            : base(Matrix, false)
        {
        }
    }
}
