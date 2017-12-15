// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Converts the colors of the image recreating an old Kodachrome camera effect.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class KodachromeProcessor<TPixel> : ColorMatrixProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4
        {
            M11 = 0.6997023F,
            M22 = 0.4609577F,
            M33 = 0.397218F,
            M41 = 0.005F,
            M42 = -0.005F,
            M43 = 0.005F,
            M44 = 1
        };
    }
}