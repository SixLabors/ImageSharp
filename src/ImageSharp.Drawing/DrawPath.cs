// <copyright file="DrawPath.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using Drawing;
    using Drawing.Brushes;
    using Drawing.Pens;
    using Drawing.Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Draws the outline of the region with the provided pen.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="path">The path.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Draw<TColor>(this Image<TColor> source, IPen<TColor> pen, Drawable path, GraphicsOptions options)
           where TColor : struct, IPixel<TColor>
        {
            return source.Apply(new DrawPathProcessor<TColor>(pen, path, options));
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided pen.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="path">The path.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Draw<TColor>(this Image<TColor> source, IPen<TColor> pen, Drawable path)
           where TColor : struct, IPixel<TColor>
        {
            return source.Draw(pen, path, GraphicsOptions.Default);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="path">The path.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Draw<TColor>(this Image<TColor> source, IBrush<TColor> brush, float thickness, Drawable path, GraphicsOptions options)
           where TColor : struct, IPixel<TColor>
        {
            return source.Draw(new Pen<TColor>(brush, thickness), path, options);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="path">The path.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Draw<TColor>(this Image<TColor> source, IBrush<TColor> brush, float thickness, Drawable path)
           where TColor : struct, IPixel<TColor>
        {
            return source.Draw(new Pen<TColor>(brush, thickness), path);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="path">The path.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Draw<TColor>(this Image<TColor> source, TColor color, float thickness, Drawable path, GraphicsOptions options)
           where TColor : struct, IPixel<TColor>
        {
            return source.Draw(new SolidBrush<TColor>(color), thickness, path, options);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="path">The path.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Draw<TColor>(this Image<TColor> source, TColor color, float thickness, Drawable path)
           where TColor : struct, IPixel<TColor>
        {
            return source.Draw(new SolidBrush<TColor>(color), thickness, path);
        }
    }
}
