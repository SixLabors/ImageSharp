// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Encapsulates properties and methods for creating processors that utilize a matrix to
    /// alter the image pixels.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal interface IColorMatrixProcessor<TPixel> : IImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the <see cref="Matrix4x4"/> used to alter the image.
        /// </summary>
        Matrix4x4 Matrix { get; }

        /// <summary>
        /// Gets or sets a value indicating whether to compress or expand individual pixel color values on processing.
        /// </summary>
        bool Compand { get; set; }
    }
}
