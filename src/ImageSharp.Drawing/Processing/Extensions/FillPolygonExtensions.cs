// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Primitives;
using SixLabors.Shapes;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the filling of closed linear polygons to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class FillPolygonExtensions
    {
        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext FillPolygon(
            this IImageProcessingContext source,
            GraphicsOptions options,
            IBrush brush,
            params PointF[] points) =>
            source.Fill(options, brush, new Polygon(new LinearLineSegment(points)));

        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext FillPolygon(
            this IImageProcessingContext source,
            IBrush brush,
            params PointF[] points) =>
            source.Fill(brush, new Polygon(new LinearLineSegment(points)));

        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="color">The color.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext FillPolygon(
            this IImageProcessingContext source,
            GraphicsOptions options,
            Color color,
            params PointF[] points) =>
            source.Fill(options, new SolidBrush(color), new Polygon(new LinearLineSegment(points)));

        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext FillPolygon(
            this IImageProcessingContext source,
            Color color,
            params PointF[] points) =>
            source.Fill(new SolidBrush(color), new Polygon(new LinearLineSegment(points)));
    }
}