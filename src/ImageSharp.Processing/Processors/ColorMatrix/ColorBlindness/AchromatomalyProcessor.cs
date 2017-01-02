// <copyright file="AchromatomalyProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating Achromatomaly (Color desensitivity) color blindness.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class AchromatomalyProcessor<TColor> : ColorMatrixFilter<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = .618F,
            M12 = .163F,
            M13 = .163F,
            M21 = .320F,
            M22 = .775F,
            M23 = .320F,
            M31 = .062F,
            M32 = .062F,
            M33 = .516F,
            M44 = 1
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}
