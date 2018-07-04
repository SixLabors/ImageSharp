// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds box blurring extensions to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class BoxBlurExtensions
    {
        /// <summary>
        /// Applies a box blur to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BoxBlur<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BoxBlurProcessor<TPixel>());

        /// <summary>
        /// Applies a box blur to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BoxBlur<TPixel>(this IImageProcessingContext<TPixel> source, int radius)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BoxBlurProcessor<TPixel>(radius));

        /// <summary>
        /// Applies a box blur to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BoxBlur<TPixel>(this IImageProcessingContext<TPixel> source, int radius, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BoxBlurProcessor<TPixel>(radius), rectangle);
    }
}