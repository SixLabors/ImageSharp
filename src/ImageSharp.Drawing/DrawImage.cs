// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Drawing.Processors;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="location">The location to draw the blended image.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DrawImage<TPixel>(this IImageProcessingContext<TPixel> source, Image<TPixel> image, Point location, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            source.ApplyProcessor(new DrawImageProcessor<TPixel>(image, location, options));
            return source;
        }

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Blend<TPixel>(this IImageProcessingContext<TPixel> source, Image<TPixel> image, float opacity)
            where TPixel : struct, IPixel<TPixel>
        {
            GraphicsOptions options = GraphicsOptions.Default;
            options.BlendPercentage = opacity;
            return DrawImage(source, image, Point.Empty, options);
        }

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="blender">The blending mode.</param>
        /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Blend<TPixel>(this IImageProcessingContext<TPixel> source, Image<TPixel> image, PixelBlenderMode blender, float opacity)
            where TPixel : struct, IPixel<TPixel>
        {
            GraphicsOptions options = GraphicsOptions.Default;
            options.BlendPercentage = opacity;
            options.BlenderMode = blender;
            return DrawImage(source, image, Point.Empty, options);
        }

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="options">The options, including the blending type and blending amount.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Blend<TPixel>(this IImageProcessingContext<TPixel> source, Image<TPixel> image, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            return DrawImage(source, image, Point.Empty, options);
        }

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
        {
            GraphicsOptions options = GraphicsOptions.Default;
            options.BlendPercentage = opacity;
            return source.DrawImage(image, location, options);
        }

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
        public static IImageProcessingContext<TPixel> DrawImage<TPixel>(this IImageProcessingContext<TPixel> source, Image<TPixel> image, PixelBlenderMode blender, float opacity, Point location)
            where TPixel : struct, IPixel<TPixel>
        {
            GraphicsOptions options = GraphicsOptions.Default;
            options.BlenderMode = blender;
            options.BlendPercentage = opacity;
            return source.DrawImage(image,  location, options);
        }
    }
}