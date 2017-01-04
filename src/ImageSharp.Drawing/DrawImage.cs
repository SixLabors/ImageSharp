// <copyright file="DrawImage.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using Drawing.Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="percent">The opacity of the image image to blend. Must be between 0 and 100.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Blend<TColor>(this Image<TColor> source, Image<TColor> image, int percent = 50)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return DrawImage(source, image, percent, default(Size), default(Point));
        }

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="percent">The opacity of the image image to blend. Must be between 0 and 100.</param>
        /// <param name="size">The size to draw the blended image.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> DrawImage<TColor>(this Image<TColor> source, Image<TColor> image, int percent, Size size, Point location)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            if (size == default(Size))
            {
                size = new Size(image.Width, image.Height);
            }

            if (location == default(Point))
            {
                location = Point.Empty;
            }

            return source.Apply(source.Bounds, new DrawImageProcessor<TColor>(image, size, location, percent));
        }
    }
}