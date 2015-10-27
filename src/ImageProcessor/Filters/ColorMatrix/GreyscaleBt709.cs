// <copyright file="GreyscaleBt709.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    /// <summary>
    /// Converts the colors of the image to greyscale applying the formula as specified by
    /// ITU-R Recommendation BT.709 <see href="https://en.wikipedia.org/wiki/Rec._709#Luma_coefficients"/>.
    /// </summary>
    public class GreyscaleBt709 : ColorMatrixFilter
    {
        /// <summary>
        /// The inversion matrix.
        /// </summary>
        private static readonly ColorMatrix Matrix = new ColorMatrix(
            new[]
                {
                    new float[] { 0.2126f, 0.2126f, 0.2126f, 0, 0 },
                    new float[] { 0.7152f, 0.7152f, 0.7152f, 0, 0 },
                    new float[] { 0.0722f, 0.0722f, 0.0722f, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 0, 0, 0, 0, 1 }
                });

        /// <summary>
        /// Initializes a new instance of the <see cref="GreyscaleBt709"/> class.
        /// </summary>
        public GreyscaleBt709()
            : base(Matrix, true)
        {
        }
    }
}
