// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds Gaussian sharpening extensions to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class GaussianSharpenExtensions
    {
        /// <summary>
        /// Applies a Gaussian sharpening filter to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> GaussianSharpen<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
        => source.ApplyProcessor(new GaussianSharpenProcessor<TPixel>());

        /// <summary>
        /// Applies a Gaussian sharpening filter to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> GaussianSharpen<TPixel>(this IImageProcessingContext<TPixel> source, float sigma)
            where TPixel : struct, IPixel<TPixel>
        => source.ApplyProcessor(new GaussianSharpenProcessor<TPixel>(sigma));

        /// <summary>
        /// Applies a Gaussian sharpening filter to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> GaussianSharpen<TPixel>(this IImageProcessingContext<TPixel> source, float sigma, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        => source.ApplyProcessor(new GaussianSharpenProcessor<TPixel>(sigma), rectangle);
    }
}
