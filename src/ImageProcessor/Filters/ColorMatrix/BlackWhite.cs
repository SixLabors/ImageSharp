// <copyright file="BlackWhite.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    /// <summary>
    /// Converts the colors of the image to their black and white equivalent.
    /// </summary>
    public class BlackWhite : ColorMatrixFilter
    {
        /// <summary>
        /// The BlackWhite matrix.
        /// TODO: Calculate a matrix that works in the linear color space.
        /// </summary>
        private static readonly ColorMatrix Matrix = new ColorMatrix(
            new[]
                {
                    new[] { 1.5f, 1.5f, 1.5f, 0, 0 },
                    new[] { 1.5f, 1.5f, 1.5f, 0, 0 },
                    new[] { 1.5f, 1.5f, 1.5f, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { -1, -1, -1, 0, 1 }
                });

        /// <summary>
        /// Initializes a new instance of the <see cref="BlackWhite"/> class.
        /// </summary>
        public BlackWhite()
            : base(Matrix, false)
        {
        }
    }
}
