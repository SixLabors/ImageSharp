// <copyright file="Fill.cs" company="James Jackson-South">
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
    using Drawing.Processors;
    using Drawing.Shapes;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Flood fills the image with the specified brush.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, IBrush<TColor> brush)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Apply(new FillProcessor<TColor>(brush));
        }

        /// <summary>
        /// Flood fills the image with the specified color.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, TColor color)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Fill(new SolidBrush<TColor>(color));
        }

        /// <summary>
        /// Flood fills the image in the shape o fhte provided polygon with the specified brush..
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The graphics options.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, IBrush<TColor> brush, IShape shape, GraphicsOptions options)
          where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Apply(new FillShapeProcessor<TColor>(brush, shape, options));
        }

        /// <summary>
        /// Flood fills the image in the shape of the provided polygon with the specified brush.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, IBrush<TColor> brush, IShape shape)
          where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Apply(new FillShapeProcessor<TColor>(brush, shape, GraphicsOptions.Default));
        }

        /// <summary>
        /// Flood fills the image in the shape o fhte provided polygon with the specified brush..
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, TColor color, IShape shape, GraphicsOptions options)
          where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Fill(new SolidBrush<TColor>(color), shape, options);
        }

        /// <summary>
        /// Flood fills the image in the shape o fhte provided polygon with the specified brush..
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, TColor color, IShape shape)
          where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Fill(new SolidBrush<TColor>(color), shape);
        }

        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> FillPolygon<TColor>(this Image<TColor> source, IBrush<TColor> brush, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            // using Polygon directly instead of LinearPolygon as its will have less indirection
            return source.Fill(brush, new Polygon(new LinearLineSegment(points)), options);
        }

        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="points">The points.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> FillPolygon<TColor>(this Image<TColor> source, IBrush<TColor> brush, Vector2[] points)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            // using Polygon directly instead of LinearPolygon as its will have less indirection
            return source.Fill(brush, new Polygon(new LinearLineSegment(points)));
        }

        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> FillPolygon<TColor>(this Image<TColor> source, TColor color, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            // using Polygon directly instead of LinearPolygon as its will have less indirection
            return source.Fill(new SolidBrush<TColor>(color), new Polygon(new LinearLineSegment(points)), options);
        }

        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="points">The points.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> FillPolygon<TColor>(this Image<TColor> source, TColor color, Vector2[] points)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            // using Polygon directly instead of LinearPolygon as its will have less indirection
            return source.Fill(new SolidBrush<TColor>(color), new Polygon(new LinearLineSegment(points)));
        }
    }
}
