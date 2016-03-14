// <copyright file="Deuteranopia.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating Deuteranopia (Green-Blind) color blindness.
    /// </summary>
    public class Deuteranopia : ColorMatrixFilter
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 0.625f,
            M12 = 0.7f,
            M21 = 0.375f,
            M22 = 0.3f,
            M23 = 0.3f,
            M33 = 0.7f
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}
