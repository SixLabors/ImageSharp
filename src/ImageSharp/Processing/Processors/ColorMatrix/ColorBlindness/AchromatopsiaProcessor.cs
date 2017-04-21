// <copyright file="AchromatopsiaProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;

    using ImageSharp.PixelFormats;

    /// <summary>
    /// Converts the colors of the image recreating Achromatopsia (Monochrome) color blindness.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class AchromatopsiaProcessor<TPixel> : ColorMatrixProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
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

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}
