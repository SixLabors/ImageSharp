// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the alteration of the hue component to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class HueExtensions
    {
        /// <summary>
        /// Alters the hue component of the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="degrees">The rotation angle in degrees to adjust the hue.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Hue<TPixel>(this IImageProcessingContext<TPixel> source, float degrees)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new HueProcessor<TPixel>(degrees));

        /// <summary>
        /// Alters the hue component of the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="degrees">The rotation angle in degrees to adjust the hue.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Hue<TPixel>(this IImageProcessingContext<TPixel> source, float degrees, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new HueProcessor<TPixel>(degrees), rectangle);
    }
}