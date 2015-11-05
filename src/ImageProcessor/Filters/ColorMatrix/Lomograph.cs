// <copyright file="Lomograph.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating an old Lomograph effect.
    /// </summary>
    public class Lomograph : ColorMatrixFilter
    {
        /// <summary>
        /// The Lomograph matrix. Purely artistic in composition.
        /// </summary>
        private static readonly Matrix4x4 ColorMatrix = new Matrix4x4()
        {
            M11 = 1.5f,
            M22 = 1.45f,
            M33 = 1.11f,
            M41 = -.1f,
            M42 = .0f,
            M43 = -.08f
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="Lomograph"/> class.
        /// </summary>
        public Lomograph()
            : base(ColorMatrix)
        {
        }
    }
}
