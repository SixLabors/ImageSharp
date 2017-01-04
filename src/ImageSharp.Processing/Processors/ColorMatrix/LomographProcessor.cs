// <copyright file="LomographProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating an old Lomograph effect.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class LomographProcessor<TColor> : ColorMatrixFilter<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 1.5F,
            M22 = 1.45F,
            M33 = 1.11F,
            M41 = -.1F,
            M42 = .0F,
            M43 = -.08F,
            M44 = 1
        };

        /// <inheritdoc/>
        protected override void AfterApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            TColor packed = default(TColor);
            packed.PackFromVector4(new Color(0, 10, 0).ToVector4()); // Very dark (mostly black) lime green.
            new VignetteProcessor<TColor> { VignetteColor = packed }.Apply(source, sourceRectangle);
        }
    }
}