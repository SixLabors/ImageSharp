// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Primitives;
using SixLabors.Shapes;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the drawing of rectangles to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class DrawRectangleExtensions
    {
        /// <summary>
        /// Draws the outline of the rectangle with the provided pen.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Draw(
            this IImageProcessingContext source,
            GraphicsOptions options,
            IPen pen,
            RectangleF shape) =>
            source.Draw(options, pen, new RectangularPolygon(shape.X, shape.Y, shape.Width, shape.Height));

        /// <summary>
        /// Draws the outline of the rectangle with the provided pen.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Draw(this IImageProcessingContext source, IPen pen, RectangleF shape) =>
            source.Draw(GraphicsOptions.Default, pen, shape);

        /// <summary>
        /// Draws the outline of the rectangle with the provided brush at the provided thickness.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Draw(
            this IImageProcessingContext source,
            GraphicsOptions options,
            IBrush brush,
            float thickness,
            RectangleF shape) =>
            source.Draw(options, new Pen(brush, thickness), shape);

        /// <summary>
        /// Draws the outline of the rectangle with the provided brush at the provided thickness.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Draw(
            this IImageProcessingContext source,
            IBrush brush,
            float thickness,
            RectangleF shape) =>
            source.Draw(new Pen(brush, thickness), shape);

        /// <summary>
        /// Draws the outline of the rectangle with the provided brush at the provided thickness.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Draw(
            this IImageProcessingContext source,
            GraphicsOptions options,
            Color color,
            float thickness,
            RectangleF shape) =>
            source.Draw(options, new SolidBrush(color), thickness, shape);

        /// <summary>
        /// Draws the outline of the rectangle with the provided brush at the provided thickness.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="shape">The shape.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Draw(
            this IImageProcessingContext source,
            Color color,
            float thickness,
            RectangleF shape) =>
            source.Draw(new SolidBrush(color), thickness, shape);
    }
}