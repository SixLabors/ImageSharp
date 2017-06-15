// <copyright file="DrawImage.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using Drawing.Processors;
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;

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
        /// <param name="size">The size to draw the blended image.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> DrawImage<TPixel>(this Image<TPixel> source, Image<TPixel> image, Size size, Point location, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            if (size == default(Size))
            {
                size = new Size(image.Width, image.Height);
            }

            if (location == default(Point))
            {
                location = Point.Empty;
            }

            source.ApplyProcessor(new DrawImageProcessor<TPixel>(image, size, location, options), source.Bounds);
            return source;
        }

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="percent">The opacity of the image image to blend. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Blend<TPixel>(this Image<TPixel> source, Image<TPixel> image, float percent)
            where TPixel : struct, IPixel<TPixel>
        {
            GraphicsOptions options = GraphicsOptions.Default;
            options.BlendPercentage = percent;
            return DrawImage(source, image, default(Size), default(Point), options);
        }

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="blender">The blending mode.</param>
        /// <param name="percent">The opacity of the image image to blend. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Blend<TPixel>(this Image<TPixel> source, Image<TPixel> image, PixelBlenderMode blender, float percent)
            where TPixel : struct, IPixel<TPixel>
        {
            GraphicsOptions options = GraphicsOptions.Default;
            options.BlendPercentage = percent;
            options.BlenderMode = blender;
            return DrawImage(source, image, default(Size), default(Point), options);
        }

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="options">The options, including the blending type and belnding amount.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Blend<TPixel>(this Image<TPixel> source, Image<TPixel> image, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            return DrawImage(source, image, default(Size), default(Point), options);
        }

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="percent">The opacity of the image image to blend. Must be between 0 and 1.</param>
        /// <param name="size">The size to draw the blended image.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> DrawImage<TPixel>(this Image<TPixel> source, Image<TPixel> image, float percent, Size size, Point location)
            where TPixel : struct, IPixel<TPixel>
        {
            GraphicsOptions options = GraphicsOptions.Default;
            options.BlendPercentage = percent;
            return source.DrawImage(image, size, location, options);
        }

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="blender">The type of bending to apply.</param>
        /// <param name="percent">The opacity of the image image to blend. Must be between 0 and 1.</param>
        /// <param name="size">The size to draw the blended image.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> DrawImage<TPixel>(this Image<TPixel> source, Image<TPixel> image, PixelBlenderMode blender, float percent, Size size, Point location)
            where TPixel : struct, IPixel<TPixel>
        {
            GraphicsOptions options = GraphicsOptions.Default;
            options.BlenderMode = blender;
            options.BlendPercentage = percent;
            return source.DrawImage(image, size, location, options);
        }
    }
}