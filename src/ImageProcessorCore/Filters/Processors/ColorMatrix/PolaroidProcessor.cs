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
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class PolaroidProcessor<T, TP> : ColorMatrixFilter<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
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
        protected override void AfterApply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            T packedV = default(T);
            packedV.PackBytes(102, 34, 0, 255);
            new VignetteProcessor<T, TP> { VignetteColor = packedV }.Apply(target, target, targetRectangle);

            T packedG = default(T);
            packedG.PackBytes(255, 153, 102, 178);
            new GlowProcessor<T, TP>
            {
                GlowColor = packedG,
                RadiusX = target.Width / 4f,
                RadiusY = target.Width / 4f
            }
            .Apply(target, target, targetRectangle);
        }
    }
}
