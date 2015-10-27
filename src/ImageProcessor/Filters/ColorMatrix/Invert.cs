// <copyright file="Invert.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    /// <summary>
    /// Inverts the colors of the image.
    /// </summary>
    public class Invert : ColorMatrixFilter
    {
        /// <summary>
        /// The inversion matrix.
        /// TODO: With gamma adjustment enabled this leaves the image too bright.
        /// </summary>
        private static readonly ColorMatrix Matrix = new ColorMatrix(
            new[]
                {
                    new float[] { -1, 0, 0, 0, 0 },
                    new float[] { 0, -1, 0, 0, 0 },
                    new float[] { 0, 0, -1, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 1, 1, 1, 0, 1 }
                });

        /// <summary>
        /// Initializes a new instance of the <see cref="Invert"/> class.
        /// </summary>
        public Invert()
            : base(Matrix, false)
        {
        }
    }
}
