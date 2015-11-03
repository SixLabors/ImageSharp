// <copyright file="Polaroid.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating an old Polaroid effect.
    /// </summary>
    public class Polaroid : ColorMatrixFilter
    {
        /// <summary>
        /// The Polaroid matrix. Purely artistic in composition.
        /// TODO: Calculate a matrix that works in the linear color space.
        /// </summary>
        private static readonly Matrix4x4 Matrix = new Matrix4x4()
        {
            M11 = 1.538f,
            M12 = -0.062f,
            M13 = -0.262f,
            M21 = -0.022f,
            M22 = 1.578f,
            M23 = -0.022f,
            M31 = .616f,
            M32 = -.16f,
            M33 = 1.5831f,
            M41 = 0.02f,
            M42 = -0.05f,
            M43 = -0.05f
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="Polaroid"/> class.
        /// </summary>
        public Polaroid()
            : base(Matrix)
        {
        }
    }
}
