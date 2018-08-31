// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the alteration of the saturation component to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class SaturateExtensions
    {
        /// <summary>
        /// Alters the saturation component of the image.
        /// </summary>
        /// <remarks>
        /// A value of 0 is completely un-saturated. A value of 1 leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of amount over 1 are allowed, providing super-saturated results
        /// </remarks>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Saturate<TPixel>(this IImageProcessingContext<TPixel> source, float amount)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new SaturateProcessor<TPixel>(amount));

        /// <summary>
        /// Alters the saturation component of the image.
        /// </summary>
        /// <remarks>
        /// A value of 0 is completely un-saturated. A value of 1 leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of amount over 1 are allowed, providing super-saturated results
        /// </remarks>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Saturate<TPixel>(this IImageProcessingContext<TPixel> source, float amount, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new SaturateProcessor<TPixel>(amount), rectangle);
    }
}