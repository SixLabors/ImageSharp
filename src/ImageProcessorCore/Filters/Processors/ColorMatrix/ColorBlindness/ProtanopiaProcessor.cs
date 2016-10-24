// <copyright file="ProtanopiaProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Numerics;

    /// <summary>
    /// Converts the colors of the image recreating Protanopia (Red-Blind) color blindness.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class ProtanopiaProcessor<TColor, TPacked> : ColorMatrixFilter<TColor, TPacked>
        where TColor : struct, IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 0.567f,
            M12 = 0.558f,
            M21 = 0.433f,
            M22 = 0.442f,
            M23 = 0.242f,
            M33 = 0.758f
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}
