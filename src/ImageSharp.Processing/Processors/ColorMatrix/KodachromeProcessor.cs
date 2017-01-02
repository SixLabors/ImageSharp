// <copyright file="KodachromeProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating an old Kodachrome camera effect.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class KodachromeProcessor<TColor> : ColorMatrixFilter<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 0.6997023F,
            M22 = 0.4609577F,
            M33 = 0.397218F,
            M41 = 0.005F,
            M42 = -0.005F,
            M43 = 0.005F,
            M44 = 1
        };
    }
}