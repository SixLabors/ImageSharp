// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Converts the colors of the image to Grayscale applying the formula as specified by ITU-R Recommendation BT.709
    /// <see href="https://en.wikipedia.org/wiki/Rec._709#Luma_coefficients"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class GrayscaleBt709Processor<TPixel> : ColorMatrixProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4
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