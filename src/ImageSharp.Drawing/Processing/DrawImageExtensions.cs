// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Drawing;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the drawing of images to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class DrawImageExtensions
    {
        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DrawImage<TPixel>(this IImageProcessingContext<TPixel> source, Image<TPixel> image, float opacity)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new DrawImageProcessor<TPixel>(image, opacity));

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="blender">The blending mode.</param>
        /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DrawImage<TPixel>(this IImageProcessingContext<TPixel> source, Image<TPixel> image, PixelColorBlendingMode blender, float opacity)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new DrawImageProcessor<TPixel>(image, opacity, blender));

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options, including the blending type and blending amount.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DrawImage<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new DrawImageProcessor<TPixel>(image, options));

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DrawImage<TPixel>(this IImageProcessingContext<TPixel> source, Image<TPixel> image, float opacity, Point location)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new DrawImageProcessor<TPixel>(image, location, opacity));

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="blender">The type of bending to apply.</param>
        /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DrawImage<TPixel>(this IImageProcessingContext<TPixel> source, Image<TPixel> image, PixelColorBlendingMode blender, float opacity, Point location)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new DrawImageProcessor<TPixel>(image, location, opacity, blender));

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options containing the blend mode and opacity.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="location">The location to draw the blended image.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DrawImage<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, Image<TPixel> image, Point location)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new DrawImageProcessor<TPixel>(image, location, options));
    }
}