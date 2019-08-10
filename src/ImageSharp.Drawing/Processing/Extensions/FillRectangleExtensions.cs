// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Primitives;
using SixLabors.Shapes;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the filling of rectangles to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class FillRectangleExtensions
    {
        /// <summary>
        /// Flood fills the image in the shape of the provided rectangle with the specified brush.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Fill(
            this IImageProcessingContext source,
            GraphicsOptions options,
            IBrush brush,
            RectangleF shape) =>
            source.Fill(options, brush, new RectangularPolygon(shape.X, shape.Y, shape.Width, shape.Height));

        /// <summary>
        /// Flood fills the image in the shape of the provided rectangle with the specified brush.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext
            Fill(this IImageProcessingContext source, IBrush brush, RectangleF shape) =>
            source.Fill(brush, new RectangularPolygon(shape.X, shape.Y, shape.Width, shape.Height));

        /// <summary>
        /// Flood fills the image in the shape of the provided rectangle with the specified brush.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="color">The color.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Fill(
            this IImageProcessingContext source,
            GraphicsOptions options,
            Color color,
            RectangleF shape) =>
            source.Fill(options, new SolidBrush(color), shape);

        /// <summary>
        /// Flood fills the image in the shape of the provided rectangle with the specified brush.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext
            Fill(this IImageProcessingContext source, Color color, RectangleF shape) =>
            source.Fill(new SolidBrush(color), shape);
    }
}