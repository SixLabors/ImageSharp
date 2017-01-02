// <copyright file="GrayscaleBt709Processor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image to Grayscale applying the formula as specified by
    /// ITU-R Recommendation BT.709 <see href="https://en.wikipedia.org/wiki/Rec._709#Luma_coefficients"/>.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class GrayscaleBt709Processor<TColor> : ColorMatrixFilter<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = .2126F,
            M12 = .2126F,
            M13 = .2126F,
            M21 = .7152F,
            M22 = .7152F,
            M23 = .7152F,
            M31 = .0722F,
            M32 = .0722F,
            M33 = .0722F,
            M44 = 1
        };
    }
}