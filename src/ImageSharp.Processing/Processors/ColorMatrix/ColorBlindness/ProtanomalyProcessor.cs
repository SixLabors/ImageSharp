// <copyright file="ProtanomalyProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating Protanopia (Red-Weak) color blindness.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class ProtanomalyProcessor<TColor> : ColorMatrixFilter<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 0.817F,
            M12 = 0.333F,
            M21 = 0.183F,
            M22 = 0.667F,
            M23 = 0.125F,
            M33 = 0.875F,
            M44 = 1
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}