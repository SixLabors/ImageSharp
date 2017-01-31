// <copyright file="DrawPolygon.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;
    using Drawing;
    using Drawing.Brushes;
    using Drawing.Pens;
    using Drawing.Processors;
    using SixLabors.Shapes;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Draws the provided Points as a closed Linear Polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> DrawPolygon<TColor>(this Image<TColor> source, IBrush<TColor> brush, float thickness, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Draw(new Pen<TColor>(brush, thickness), new Polygon(new LinearLineSegment(points)), options);
        }

        /// <summary>
        /// Draws the provided Points as a closed Linear Polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> DrawPolygon<TColor>(this Image<TColor> source, IBrush<TColor> brush, float thickness, Vector2[] points)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Draw(new Pen<TColor>(brush, thickness), new Polygon(new LinearLineSegment(points)));
        }

        /// <summary>
        /// Draws the provided Points as a closed Linear Polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> DrawPolygon<TColor>(this Image<TColor> source, TColor color, float thickness, Vector2[] points)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPolygon(new SolidBrush<TColor>(color), thickness, points);
        }

        /// <summary>
        /// Draws the provided Points as a closed Linear Polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> DrawPolygon<TColor>(this Image<TColor> source, TColor color, float thickness, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPolygon(new SolidBrush<TColor>(color), thickness, points, options);
        }

        /// <summary>
        /// Draws the provided Points as a closed Linear Polygon with the provided Pen.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> DrawPolygon<TColor>(this Image<TColor> source, IPen<TColor> pen, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Draw(pen, new Polygon(new LinearLineSegment(points)), options);
        }
    }
}
