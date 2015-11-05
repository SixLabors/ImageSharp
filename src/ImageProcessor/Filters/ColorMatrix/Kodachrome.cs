// <copyright file="Kodachrome.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating an old Kodachrome camera effect.
    /// </summary>
    public class Kodachrome : ColorMatrixFilter
    {
        /// <summary>
        /// The Kodachrome matrix. Purely artistic in composition.
        /// </summary>
        private static readonly Matrix4x4 ColorMatrix = new Matrix4x4()
        {
            M11 = 0.6997023f,
            M22 = 0.4609577f,
            M33 = 0.397218f,
            M41 = 0.005f,
            M42 = -0.005f,
            M43 = 0.005f
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="Kodachrome"/> class.
        /// </summary>
        public Kodachrome()
            : base(ColorMatrix)
        {
        }
    }
}
