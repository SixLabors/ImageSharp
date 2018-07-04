// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Effects;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds pixelation effect extensions to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class PixelateExtensions
    {
        /// <summary>
        /// Pixelates an image with the given pixel size.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Pixelate<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => Pixelate(source, 4);

        /// <summary>
        /// Pixelates an image with the given pixel size.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="size">The size of the pixels.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Pixelate<TPixel>(this IImageProcessingContext<TPixel> source, int size)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new PixelateProcessor<TPixel>(size));

        /// <summary>
        /// Pixelates an image with the given pixel size.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="size">The size of the pixels.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Pixelate<TPixel>(this IImageProcessingContext<TPixel> source, int size, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new PixelateProcessor<TPixel>(size), rectangle);
    }
}