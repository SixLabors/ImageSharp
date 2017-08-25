// <copyright file="DrawBeziers.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Numerics;
    using Drawing;
    using Drawing.Brushes;
    using Drawing.Pens;
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;
    using SixLabors.Shapes;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Draws the provided Points as an open Bezier path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> DrawBeziers<TPixel>(this Image<TPixel> source, IBrush<TPixel> brush, float thickness, PointF[] points, GraphicsOptions options)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.Draw(new Pen<TPixel>(brush, thickness), new Path(new CubicBezierLineSegment(points)), options);
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> DrawBeziers<TPixel>(this Image<TPixel> source, IBrush<TPixel> brush, float thickness, PointF[] points)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.Draw(new Pen<TPixel>(brush, thickness), new Path(new CubicBezierLineSegment(points)));
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> DrawBeziers<TPixel>(this Image<TPixel> source, TPixel color, float thickness, PointF[] points)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawBeziers(new SolidBrush<TPixel>(color), thickness, points);
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> DrawBeziers<TPixel>(this Image<TPixel> source, TPixel color, float thickness, PointF[] points, GraphicsOptions options)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawBeziers(new SolidBrush<TPixel>(color), thickness, points, options);
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path with the supplied pen
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> DrawBeziers<TPixel>(this Image<TPixel> source, IPen<TPixel> pen, PointF[] points, GraphicsOptions options)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.Draw(pen, new Path(new CubicBezierLineSegment(points)), options);
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path with the supplied pen
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> DrawBeziers<TPixel>(this Image<TPixel> source, IPen<TPixel> pen, PointF[] points)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.Draw(pen, new Path(new CubicBezierLineSegment(points)));
        }
    }
}
