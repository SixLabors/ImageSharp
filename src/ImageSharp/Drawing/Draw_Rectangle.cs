// <copyright file="Draw_Rectangle.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;
    using Drawing;
    using Drawing.Brushes;
    using Drawing.Paths;
    using Drawing.Pens;
    using Drawing.Processors;
    using Drawing.Shapes;
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor, TPacked}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Draws the outline of the polygon with the provided pen.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <typeparam name="TPacked">The type of the packed.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor, TPacked> DrawPolygon<TColor, TPacked>(this Image<TColor, TPacked> source, IPen<TColor, TPacked> pen, RectangleF shape, GraphicsOptions options)
           where TColor : struct, IPackedPixel<TPacked>
           where TPacked : struct, IEquatable<TPacked>
        {
            return source.Process(new DrawPathProcessor<TColor, TPacked>(pen, (IPath)new RectangularPolygon(shape), options));
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided pen.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <typeparam name="TPacked">The type of the packed.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The Image</returns>
        public static Image<TColor, TPacked> DrawPolygon<TColor, TPacked>(this Image<TColor, TPacked> source, IPen<TColor, TPacked> pen, RectangleF shape)
           where TColor : struct, IPackedPixel<TPacked>
           where TPacked : struct, IEquatable<TPacked>
        {
            return source.DrawPolygon(pen, shape, GraphicsOptions.Default);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <typeparam name="TPacked">The type of the packed.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor, TPacked> DrawPolygon<TColor, TPacked>(this Image<TColor, TPacked> source, IBrush<TColor, TPacked> brush, float thickness, RectangleF shape, GraphicsOptions options)
           where TColor : struct, IPackedPixel<TPacked>
           where TPacked : struct, IEquatable<TPacked>
        {
            return source.DrawPolygon(new Pen<TColor, TPacked>(brush, thickness), shape, options);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <typeparam name="TPacked">The type of the packed.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The Image</returns>
        public static Image<TColor, TPacked> DrawPolygon<TColor, TPacked>(this Image<TColor, TPacked> source, IBrush<TColor, TPacked> brush, float thickness, RectangleF shape)
           where TColor : struct, IPackedPixel<TPacked>
           where TPacked : struct, IEquatable<TPacked>
        {
            return source.DrawPolygon(new Pen<TColor, TPacked>(brush, thickness), shape);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <typeparam name="TPacked">The type of the packed.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor, TPacked> DrawPolygon<TColor, TPacked>(this Image<TColor, TPacked> source, TColor color, float thickness, RectangleF shape, GraphicsOptions options)
           where TColor : struct, IPackedPixel<TPacked>
           where TPacked : struct, IEquatable<TPacked>
        {
            return source.DrawPolygon(new SolidBrush<TColor, TPacked>(color), thickness, shape, options);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <typeparam name="TPacked">The type of the packed.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The Image</returns>
        public static Image<TColor, TPacked> DrawPolygon<TColor, TPacked>(this Image<TColor, TPacked> source, TColor color, float thickness, RectangleF shape)
           where TColor : struct, IPackedPixel<TPacked>
           where TPacked : struct, IEquatable<TPacked>
        {
            return source.DrawPolygon(new SolidBrush<TColor, TPacked>(color), thickness, shape);
        }
    }
}
