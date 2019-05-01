// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
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
        /// <param name="source">The image this method extends.</param>
        /// <param name="matrix">The filter color matrix</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Filter(this IImageProcessingContext source, ColorMatrix matrix)
            => source.ApplyProcessor(new FilterProcessor(matrix));

        /// <summary>
        /// Filters an image but the given color matrix
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="matrix">The filter color matrix</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Filter(this IImageProcessingContext source, ColorMatrix matrix, Rectangle rectangle)
            => source.ApplyProcessor(new FilterProcessor(matrix), rectangle);
    }
}