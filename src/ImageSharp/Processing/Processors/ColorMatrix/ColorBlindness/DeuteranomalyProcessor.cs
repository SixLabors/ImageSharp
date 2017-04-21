// <copyright file="DeuteranomalyProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;

    using ImageSharp.PixelFormats;

    /// <summary>
    /// Converts the colors of the image recreating Deuteranomaly (Green-Weak) color blindness.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class DeuteranomalyProcessor<TPixel> : ColorMatrixProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 0.8F,
            M12 = 0.258F,
            M21 = 0.2F,
            M22 = 0.742F,
            M23 = 0.142F,
            M33 = 0.858F,
            M44 = 1
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}