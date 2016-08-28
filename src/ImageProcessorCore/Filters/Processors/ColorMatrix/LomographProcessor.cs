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
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class LomographProcessor<TColor, TPacked> : ColorMatrixFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
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
        protected override void AfterApply(ImageBase<TColor, TPacked> source, Rectangle sourceRectangle)
        {
            TColor packed = default(TColor);
            packed.PackFromBytes(0, 10, 0, 255); // Very dark (mostly black) lime green.
            new VignetteProcessor<TColor, TPacked> { VignetteColor = packed }.Apply(source, sourceRectangle);
        }
    }
}
