// <copyright file="DrawBeziers.cs" company="James Jackson-South">
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

    using SixLabors.Shapes;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Draws the provided Points as an open Bezier path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> DrawBeziers<TColor>(this Image<TColor> source, IBrush<TColor> brush, float thickness, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPixel<TColor>
        {
            return source.Draw(new Pen<TColor>(brush, thickness), new Path(new BezierLineSegment(points)), options);
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> DrawBeziers<TColor>(this Image<TColor> source, IBrush<TColor> brush, float thickness, Vector2[] points)
           where TColor : struct, IPixel<TColor>
        {
            return source.Draw(new Pen<TColor>(brush, thickness), new Path(new BezierLineSegment(points)));
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> DrawBeziers<TColor>(this Image<TColor> source, TColor color, float thickness, Vector2[] points)
           where TColor : struct, IPixel<TColor>
        {
            return source.DrawBeziers(new SolidBrush<TColor>(color), thickness, points);
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> DrawBeziers<TColor>(this Image<TColor> source, TColor color, float thickness, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPixel<TColor>
        {
            return source.DrawBeziers(new SolidBrush<TColor>(color), thickness, points, options);
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path with the supplied pen
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> DrawBeziers<TColor>(this Image<TColor> source, IPen<TColor> pen, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPixel<TColor>
        {
            return source.Draw(pen, new Path(new BezierLineSegment(points)), options);
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path with the supplied pen
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> DrawBeziers<TColor>(this Image<TColor> source, IPen<TColor> pen, Vector2[] points)
           where TColor : struct, IPixel<TColor>
        {
            return source.Draw(pen, new Path(new BezierLineSegment(points)));
        }
    }
}
