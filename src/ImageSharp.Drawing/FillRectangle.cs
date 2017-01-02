// <copyright file="FillRectangle.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using Drawing;
    using Drawing.Brushes;
    using Drawing.Processors;
    using Drawing.Shapes;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Flood fills the image in the shape of the provided polygon with the specified brush..
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, IBrush<TColor> brush, RectangleF shape, GraphicsOptions options)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Apply(new FillShapeProcessor<TColor>(brush, new RectangularPolygon(shape), options));
        }

        /// <summary>
        /// Flood fills the image in the shape of the provided polygon with the specified brush..
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, IBrush<TColor> brush, RectangleF shape)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Apply(new FillShapeProcessor<TColor>(brush, new RectangularPolygon(shape), GraphicsOptions.Default));
        }

        /// <summary>
        /// Flood fills the image in the shape of the provided polygon with the specified brush..
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, TColor color, RectangleF shape, GraphicsOptions options)
          where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Fill(new SolidBrush<TColor>(color), shape, options);
        }

        /// <summary>
        /// Flood fills the image in the shape of the provided polygon with the specified brush..
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The Image</returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, TColor color, RectangleF shape)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Fill(new SolidBrush<TColor>(color), shape);
        }
    }
}
