// <copyright file="Tritanomaly.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating Tritanomaly (Blue-Weak) color blindness.
    /// </summary>
    public class Tritanomaly : ColorMatrixFilter
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 0.967f,
            M21 = 0.33f,
            M22 = 0.733f,
            M23 = 0.183f,
            M32 = 0.267f,
            M33 = 0.817f
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}
