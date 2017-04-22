// <copyright file="BlackWhiteProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;

    using ImageSharp.PixelFormats;

    /// <summary>
    /// Converts the colors of the image to their black and white equivalent.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BlackWhiteProcessor<TPixel> : ColorMatrixProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4()
        {
            M11 = 1.5F,
            M12 = 1.5F,
            M13 = 1.5F,
            M21 = 1.5F,
            M22 = 1.5F,
            M23 = 1.5F,
            M31 = 1.5F,
            M32 = 1.5F,
            M33 = 1.5F,
            M41 = -1F,
            M42 = -1F,
            M43 = -1F,
            M44 = 1
        };
    }
}