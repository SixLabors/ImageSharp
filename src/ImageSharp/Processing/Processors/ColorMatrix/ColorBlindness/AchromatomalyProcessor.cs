// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Converts the colors of the image recreating Achromatomaly (Color desensitivity) color blindness.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class AchromatomalyProcessor<TPixel> : ColorMatrixProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4
        {
            M11 = .618F,
            M12 = .163F,
            M13 = .163F,
            M21 = .320F,
            M22 = .775F,
            M23 = .320F,
            M31 = .062F,
            M32 = .062F,
            M33 = .516F,
            M44 = 1
        };

        /// <inheritdoc/>
        public override bool Compand => false;
    }
}
