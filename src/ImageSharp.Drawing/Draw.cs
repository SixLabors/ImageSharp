// <copyright file="Draw.cs" company="James Jackson-South">
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

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Draws the outline of the polygon with the provided pen.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> DrawPolygon<TColor>(this Image<TColor> source, IPen<TColor> pen, IShape shape, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Apply(new DrawPathProcessor<TColor>(pen, shape, options));
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided pen.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> DrawPolygon<TColor>(this Image<TColor> source, IPen<TColor> pen, IShape shape)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPolygon(pen, shape, GraphicsOptions.Default);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> DrawPolygon<TColor>(this Image<TColor> source, IBrush<TColor> brush, float thickness, IShape shape, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPolygon(new Pen<TColor>(brush, thickness), shape, options);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> DrawPolygon<TColor>(this Image<TColor> source, IBrush<TColor> brush, float thickness, IShape shape)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPolygon(new Pen<TColor>(brush, thickness), shape);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> DrawPolygon<TColor>(this Image<TColor> source, TColor color, float thickness, IShape shape, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPolygon(new SolidBrush<TColor>(color), thickness, shape, options);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> DrawPolygon<TColor>(this Image<TColor> source, TColor color, float thickness, IShape shape)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPolygon(new SolidBrush<TColor>(color), thickness, shape);
        }

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
            return source.DrawPolygon(new Pen<TColor>(brush, thickness), new Polygon(new LinearLineSegment(points)), options);
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
            return source.DrawPolygon(new Pen<TColor>(brush, thickness), new Polygon(new LinearLineSegment(points)));
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
            return source.DrawPolygon(pen, new Polygon(new LinearLineSegment(points)), options);
        }

        /// <summary>
        /// Draws the provided Points as a closed Linear Polygon with the provided Pen.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="points">The points.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> DrawPolygon<TColor>(this Image<TColor> source, IPen<TColor> pen, Vector2[] points)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPolygon(pen, new Polygon(new LinearLineSegment(points)));
        }

        /// <summary>
        /// Draws the path with the provided pen.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="path">The path.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> DrawPath<TColor>(this Image<TColor> source, IPen<TColor> pen, IPath path, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Apply(new DrawPathProcessor<TColor>(pen, path, options));
        }

        /// <summary>
        /// Draws the path with the provided pen.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="path">The path.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> DrawPath<TColor>(this Image<TColor> source, IPen<TColor> pen, IPath path)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Apply(new DrawPathProcessor<TColor>(pen, path, GraphicsOptions.Default));
        }

        /// <summary>
        /// Draws the path with the bursh at the privdied thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="path">The path.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> DrawPath<TColor>(this Image<TColor> source, IBrush<TColor> brush, float thickness, IPath path, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPath(new Pen<TColor>(brush, thickness), path, options);
        }

        /// <summary>
        /// Draws the path with the bursh at the privdied thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="path">The path.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> DrawPath<TColor>(this Image<TColor> source, IBrush<TColor> brush, float thickness, IPath path)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPath(new Pen<TColor>(brush, thickness), path);
        }

        /// <summary>
        /// Draws the path with the bursh at the privdied thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="path">The path.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> DrawPath<TColor>(this Image<TColor> source, TColor color, float thickness, IPath path, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPath(new SolidBrush<TColor>(color), thickness, path, options);
        }

        /// <summary>
        /// Draws the path with the bursh at the privdied thickness.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="path">The path.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> DrawPath<TColor>(this Image<TColor> source, TColor color, float thickness, IPath path)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPath(new SolidBrush<TColor>(color), thickness, path);
        }

        /// <summary>
        /// Draws the provided Points as an open Linear path at the provided thickness with the supplied brush
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
        public static Image<TColor> DrawLines<TColor>(this Image<TColor> source, IBrush<TColor> brush, float thickness, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPath(new Pen<TColor>(brush, thickness), new Path(new LinearLineSegment(points)), options);
        }

        /// <summary>
        /// Draws the provided Points as an open Linear path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> DrawLines<TColor>(this Image<TColor> source, IBrush<TColor> brush, float thickness, Vector2[] points)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPath(new Pen<TColor>(brush, thickness), new Path(new LinearLineSegment(points)));
        }

        /// <summary>
        /// Draws the provided Points as an open Linear path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> DrawLines<TColor>(this Image<TColor> source, TColor color, float thickness, Vector2[] points)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawLines(new SolidBrush<TColor>(color), thickness, points);
        }

        /// <summary>
        /// Draws the provided Points as an open Linear path at the provided thickness with the supplied brush
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
        public static Image<TColor> DrawLines<TColor>(this Image<TColor> source, TColor color, float thickness, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawLines(new SolidBrush<TColor>(color), thickness, points, options);
        }

        /// <summary>
        /// Draws the provided Points as an open Linear path with the supplied pen
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> DrawLines<TColor>(this Image<TColor> source, IPen<TColor> pen, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPath(pen, new Path(new LinearLineSegment(points)), options);
        }

        /// <summary>
        /// Draws the provided Points as an open Linear path with the supplied pen
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="points">The points.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> DrawLines<TColor>(this Image<TColor> source, IPen<TColor> pen, Vector2[] points)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPath(pen, new Path(new LinearLineSegment(points)));
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path at the provided thickness with the supplied brush
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
        public static Image<TColor> DrawBeziers<TColor>(this Image<TColor> source, IBrush<TColor> brush, float thickness, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPath(new Pen<TColor>(brush, thickness), new Path(new BezierLineSegment(points)), options);
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> DrawBeziers<TColor>(this Image<TColor> source, IBrush<TColor> brush, float thickness, Vector2[] points)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPath(new Pen<TColor>(brush, thickness), new Path(new BezierLineSegment(points)));
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> DrawBeziers<TColor>(this Image<TColor> source, TColor color, float thickness, Vector2[] points)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawBeziers(new SolidBrush<TColor>(color), thickness, points);
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path at the provided thickness with the supplied brush
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
        public static Image<TColor> DrawBeziers<TColor>(this Image<TColor> source, TColor color, float thickness, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawBeziers(new SolidBrush<TColor>(color), thickness, points, options);
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path with the supplied pen
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> DrawBeziers<TColor>(this Image<TColor> source, IPen<TColor> pen, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPath(pen, new Path(new BezierLineSegment(points)), options);
        }

        /// <summary>
        /// Draws the provided Points as an open Bezier path with the supplied pen
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="points">The points.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> DrawBeziers<TColor>(this Image<TColor> source, IPen<TColor> pen, Vector2[] points)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.DrawPath(pen, new Path(new BezierLineSegment(points)));
        }
    }
}
