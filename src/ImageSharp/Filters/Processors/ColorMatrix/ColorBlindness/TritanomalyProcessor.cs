// <copyright file="TritanomalyProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processors
{
    using System;
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating Tritanomaly (Blue-Weak) color blindness.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class TritanomalyProcessor<TColor, TPacked> : ColorMatrixFilter<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct, IEquatable<TPacked>
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 0.967F,
            M21 = 0.33F,
            M22 = 0.733F,
            M23 = 0.183F,
            M32 = 0.267F,
            M33 = 0.817F,
            M44 = 1
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}