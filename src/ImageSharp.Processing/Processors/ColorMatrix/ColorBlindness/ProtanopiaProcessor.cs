// <copyright file="ProtanopiaProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating Protanopia (Red-Blind) color blindness.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class ProtanopiaProcessor<TColor> : ColorMatrixFilter<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 0.567F,
            M12 = 0.558F,
            M21 = 0.433F,
            M22 = 0.442F,
            M23 = 0.242F,
            M33 = 0.758F,
            M44 = 1
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}