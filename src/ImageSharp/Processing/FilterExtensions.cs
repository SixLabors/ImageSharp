// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the application of composable filters to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class FilterExtensions
    {
        /// <summary>
        /// Filters an image but the given color matrix
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="matrix">The filter color matrix</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Filter<TPixel>(this IImageProcessingContext<TPixel> source, Matrix4x4 matrix)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new FilterProcessor<TPixel>(matrix));

        /// <summary>
        /// Filters an image but the given color matrix
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="matrix">The filter color matrix</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Filter<TPixel>(this IImageProcessingContext<TPixel> source, Matrix4x4 matrix, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new FilterProcessor<TPixel>(matrix), rectangle);
    }
}