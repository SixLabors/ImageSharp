// <copyright file="GrayscaleBt601Processor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image to Grayscale applying the formula as specified by
    /// ITU-R Recommendation BT.601 <see href="https://en.wikipedia.org/wiki/Luma_%28video%29#Rec._601_luma_versus_Rec._709_luma_coefficients"/>.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class GrayscaleBt601Processor<TColor> : ColorMatrixFilter<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = .299F,
            M12 = .299F,
            M13 = .299F,
            M21 = .587F,
            M22 = .587F,
            M23 = .587F,
            M31 = .114F,
            M32 = .114F,
            M33 = .114F,
            M44 = 1
        };
    }
}