// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the alteration of the brightness component to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class BrightnessExtensions
    {
        /// <summary>
        /// Alters the brightness component of the image.
        /// </summary>
        /// <remarks>
        /// A value of 0 will create an image that is completely black. A value of 1 leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of an amount over 1 are allowed, providing brighter results.
        /// </remarks>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Brightness<TPixel>(this IImageProcessingContext<TPixel> source, float amount)
           where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BrightnessProcessor<TPixel>(amount));

        /// <summary>
        /// Alters the brightness component of the image.
        /// </summary>
        /// <remarks>
        /// A value of 0 will create an image that is completely black. A value of 1 leaves the input unchanged.
        /// Other values are linear multipliers on the effect. Values of an amount over 1 are allowed, providing brighter results.
        /// </remarks>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The proportion of the conversion. Must be greater than or equal to 0.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Brightness<TPixel>(this IImageProcessingContext<TPixel> source, float amount, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BrightnessProcessor<TPixel>(amount), rectangle);
    }
}
