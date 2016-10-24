// <copyright file="TritanopiaProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating Tritanopia (Blue-Blind) color blindness.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class TritanopiaProcessor<TColor, TPacked> : ColorMatrixFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 0.95f,
            M21 = 0.05f,
            M22 = 0.433f,
            M23 = 0.475f,
            M32 = 0.567f,
            M33 = 0.525f
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}
