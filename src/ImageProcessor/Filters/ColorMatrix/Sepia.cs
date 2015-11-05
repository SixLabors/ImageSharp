// <copyright file="Sepia.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image to their sepia equivalent recreating an old photo effect.
    /// </summary>
    public class Sepia : ColorMatrixFilter
    {
        /// <summary>
        /// The sepia matrix.
        /// </summary>
        private static readonly Matrix4x4 ColorMatrix = new Matrix4x4()
        {
            M11 = .393f,
            M12 = .349f,
            M13 = .272f,
            M21 = .769f,
            M22 = .686f,
            M23 = .534f,
            M31 = .189f,
            M32 = .168f,
            M33 = .131f
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="Sepia"/> class.
        /// </summary>
        public Sepia()
            : base(ColorMatrix)
        {
        }
    }
}
