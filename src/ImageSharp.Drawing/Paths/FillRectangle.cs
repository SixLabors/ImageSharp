// <copyright file="FillRectangle.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using Drawing;
    using Drawing.Brushes;
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Flood fills the image in the shape of the provided polygon with the specified brush..
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Fill<TPixel>(this Image<TPixel> source, IBrush<TPixel> brush, RectangleF shape, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            return source.Fill(brush, new SixLabors.Shapes.RectangularePolygon(shape.X, shape.Y, shape.Width, shape.Height), options);
        }

        /// <summary>
        /// Flood fills the image in the shape of the provided polygon with the specified brush..
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Fill<TPixel>(this Image<TPixel> source, IBrush<TPixel> brush, RectangleF shape)
            where TPixel : struct, IPixel<TPixel>
        {
            return source.Fill(brush, new SixLabors.Shapes.RectangularePolygon(shape.X, shape.Y, shape.Width, shape.Height));
        }

        /// <summary>
        /// Flood fills the image in the shape of the provided polygon with the specified brush..
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Fill<TPixel>(this Image<TPixel> source, TPixel color, RectangleF shape, GraphicsOptions options)
          where TPixel : struct, IPixel<TPixel>
        {
            return source.Fill(new SolidBrush<TPixel>(color), shape, options);
        }

        /// <summary>
        /// Flood fills the image in the shape of the provided polygon with the specified brush..
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Fill<TPixel>(this Image<TPixel> source, TPixel color, RectangleF shape)
            where TPixel : struct, IPixel<TPixel>
        {
            return source.Fill(new SolidBrush<TPixel>(color), shape);
        }
    }
}
