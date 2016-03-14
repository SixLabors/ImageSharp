// <copyright file="Deuteranomaly.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating Deuteranomaly (Green-Weak) color blindness.
    /// </summary>
    public class Deuteranomaly : ColorMatrixFilter
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 0.8f,
            M12 = 0.258f,
            M21 = 0.2f,
            M22 = 0.742f,
            M23 = 0.142f,
            M33 = 0.858f
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}
