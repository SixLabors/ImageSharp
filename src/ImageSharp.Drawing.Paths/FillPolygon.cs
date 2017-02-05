// <copyright file="FillPolygon.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;
    using Drawing;
    using Drawing.Brushes;
    using Drawing.Processors;

    using SixLabors.Shapes;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> FillPolygon<TColor>(this Image<TColor> source, IBrush<TColor> brush, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Fill(brush, new Polygon(new LinearLineSegment(points)), options);
        }

        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> FillPolygon<TColor>(this Image<TColor> source, IBrush<TColor> brush, Vector2[] points)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Fill(brush, new Polygon(new LinearLineSegment(points)));
        }

        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> FillPolygon<TColor>(this Image<TColor> source, TColor color, Vector2[] points, GraphicsOptions options)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Fill(new SolidBrush<TColor>(color), new Polygon(new LinearLineSegment(points)), options);
        }

        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> FillPolygon<TColor>(this Image<TColor> source, TColor color, Vector2[] points)
           where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Fill(new SolidBrush<TColor>(color), new Polygon(new LinearLineSegment(points)));
        }
    }
}
