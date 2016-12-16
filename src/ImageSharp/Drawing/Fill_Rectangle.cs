// <copyright file="Fill_Rectangle.cs" company="James Jackson-South">
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
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor, TPacked}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Flood fills the image in the shape o fhte provided polygon with the specified brush..
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <typeparam name="TPacked">The type of the packed.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor, TPacked> Fill<TColor, TPacked>(this Image<TColor, TPacked> source, IBrush<TColor, TPacked> brush, RectangleF shape, GraphicsOptions options)
          where TColor : struct, IPackedPixel<TPacked>
          where TPacked : struct, IEquatable<TPacked>
        {
            return source.Process(new FillShapeProcessor<TColor, TPacked>(brush, new RectangularPolygon(shape), options));
        }

        /// <summary>
        /// Flood fills the image in the shape o fhte provided polygon with the specified brush..
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <typeparam name="TPacked">The type of the packed.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The Image</returns>
        public static Image<TColor, TPacked> Fill<TColor, TPacked>(this Image<TColor, TPacked> source, IBrush<TColor, TPacked> brush, RectangleF shape)
          where TColor : struct, IPackedPixel<TPacked>
          where TPacked : struct, IEquatable<TPacked>
        {
            return source.Process(new FillShapeProcessor<TColor, TPacked>(brush, new RectangularPolygon(shape), GraphicsOptions.Default));
        }

        /// <summary>
        /// Flood fills the image in the shape o fhte provided polygon with the specified brush..
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <typeparam name="TPacked">The type of the packed.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The Image
        /// </returns>
        public static Image<TColor, TPacked> Fill<TColor, TPacked>(this Image<TColor, TPacked> source, TColor color, RectangleF shape, GraphicsOptions options)
          where TColor : struct, IPackedPixel<TPacked>
          where TPacked : struct, IEquatable<TPacked>
        {
            return source.Fill(new SolidBrush<TColor, TPacked>(color), shape, options);
        }

        /// <summary>
        /// Flood fills the image in the shape o fhte provided polygon with the specified brush..
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <typeparam name="TPacked">The type of the packed.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="color">The color.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The Image</returns>
        public static Image<TColor, TPacked> Fill<TColor, TPacked>(this Image<TColor, TPacked> source, TColor color, RectangleF shape)
          where TColor : struct, IPackedPixel<TPacked>
          where TPacked : struct, IEquatable<TPacked>
        {
            return source.Fill(new SolidBrush<TColor, TPacked>(color), shape);
        }
    }
}
