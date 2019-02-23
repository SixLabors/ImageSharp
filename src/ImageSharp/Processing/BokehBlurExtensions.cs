// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds bokeh blurring extensions to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class BokehBlurExtensions
    {
        /// <summary>
        /// Applies a bokeh blur to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BokehBlur<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BokehBlurProcessor<TPixel>());

        /// <summary>
        /// Applies a bokeh blur to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
        /// <param name="components">The 'components' value representing the number of kernels to use to approximate the bokeh effect.</param>
        /// <param name="gamma">The gamma highlight factor to use to emphasize bright spots in the source image</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BokehBlur<TPixel>(this IImageProcessingContext<TPixel> source, int radius, int components, float gamma)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BokehBlurProcessor<TPixel>(radius, components, gamma));

        /// <summary>
        /// Applies a bokeh blur to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BokehBlur<TPixel>(this IImageProcessingContext<TPixel> source, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BokehBlurProcessor<TPixel>(), rectangle);

        /// <summary>
        /// Applies a bokeh blur to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
        /// <param name="components">The 'components' value representing the number of kernels to use to approximate the bokeh effect.</param>
        /// <param name="gamma">The gamma highlight factor to use to emphasize bright spots in the source image</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BokehBlur<TPixel>(this IImageProcessingContext<TPixel> source, int radius, int components, float gamma, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BokehBlurProcessor<TPixel>(radius, components, gamma), rectangle);
    }
}
