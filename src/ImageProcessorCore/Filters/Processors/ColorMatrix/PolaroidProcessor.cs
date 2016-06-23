// <copyright file="PolaroidProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating an old Polaroid effect.
    /// </summary>
    public class PolaroidProcessor : ColorMatrixFilter
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 1.538f,
            M12 = -0.062f,
            M13 = -0.262f,
            M21 = -0.022f,
            M22 = 1.578f,
            M23 = -0.022f,
            M31 = .216f,
            M32 = -.16f,
            M33 = 1.5831f,
            M41 = 0.02f,
            M42 = -0.05f,
            M43 = -0.05f
        };

        /// <inheritdoc/>
        protected override void AfterApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            new VignetteProcessor { Color = new Color(102 / 255f, 34 / 255f, 0) }.Apply(target, target, targetRectangle);
            new GlowProcessor
            {
                Color = new Color(1, 153 / 255f, 102 / 255f, .7f),
                RadiusX = target.Width / 4f,
                RadiusY = target.Width / 4f
            }
            .Apply(target, target, targetRectangle);
        }
    }
}
