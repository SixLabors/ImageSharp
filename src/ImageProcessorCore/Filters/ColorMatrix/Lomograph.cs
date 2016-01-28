// <copyright file="Lomograph.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating an old Lomograph effect.
    /// </summary>
    public class Lomograph : ColorMatrixFilter
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 1.5f,
            M22 = 1.45f,
            M33 = 1.11f,
            M41 = -.1f,
            M42 = .0f,
            M43 = -.08f
        };

        /// <inheritdoc/>
        protected override void AfterApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            new Vignette { Color = new Color(0, 10 / 255f, 0) }.Apply(target, target, targetRectangle);
        }
    }
}
