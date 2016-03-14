// <copyright file="Protanomaly.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating Protanopia (Red-Weak) color blindness.
    /// </summary>
    public class Protanomaly : ColorMatrixFilter
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 0.817f,
            M12 = 0.333f,
            M21 = 0.183f,
            M22 = 0.667f,
            M23 = 0.125f,
            M33 = 0.875f
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}
