// <copyright file="LomographProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating an old Lomograph effect.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class LomographProcessor<T, TP> : ColorMatrixFilter<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
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
        protected override void AfterApply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            T packed = default(T);
            packed.PackBytes(0, 10, 0, 255);
            new VignetteProcessor<T, TP> { VignetteColor = packed }.Apply(target, target, targetRectangle);
        }
    }
}
