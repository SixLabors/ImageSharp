// <copyright file="GreyscaleBt709.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image to greyscale applying the formula as specified by
    /// ITU-R Recommendation BT.709 <see href="https://en.wikipedia.org/wiki/Rec._709#Luma_coefficients"/>.
    /// </summary>
    public class GreyscaleBt709 : ColorMatrixFilter
    {
        /// <summary>
        /// The greyscale matrix.
        /// </summary>
        private static readonly Matrix4x4 Matrix = new Matrix4x4()
        {
            M11 = .2126f,
            M12 = .2126f,
            M13 = .2126f,
            M21 = .7152f,
            M22 = .7152f,
            M23 = .7152f,
            M31 = .0722f,
            M32 = .0722f,
            M33 = .0722f
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="GreyscaleBt709"/> class.
        /// </summary>
        public GreyscaleBt709()
            : base(Matrix)
        {
        }
    }
}
