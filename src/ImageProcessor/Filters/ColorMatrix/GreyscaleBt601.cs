// <copyright file="GreyscaleBt601.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    /// <summary>
    /// Converts the colors of the image to greyscale applying the formula as specified by
    /// ITU-R Recommendation BT.601 <see href="https://en.wikipedia.org/wiki/Luma_%28video%29#Rec._601_luma_versus_Rec._709_luma_coefficients"/>.
    /// </summary>
    public class GreyscaleBt601 : ColorMatrixFilter
    {
        /// <summary>
        /// The inversion matrix.
        /// </summary>
        private static readonly ColorMatrix Matrix = new ColorMatrix(
            new[]
                {
                    new float[] { 0.299f, 0.299f, 0.299f, 0, 0 },
                    new float[] { 0.587f, 0.587f, 0.587f, 0, 0 },
                    new float[] { 0.114f, 0.114f, 0.114f, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 0, 0, 0, 0, 1 }
                });

        /// <summary>
        /// Initializes a new instance of the <see cref="GreyscaleBt601"/> class.
        /// </summary>
        public GreyscaleBt601()
            : base(Matrix, true)
        {
        }
    }
}
