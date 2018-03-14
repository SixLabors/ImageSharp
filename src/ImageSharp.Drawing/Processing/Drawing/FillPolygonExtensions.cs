// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Drawing.Brushes;
using SixLabors.Primitives;
using SixLabors.Shapes;

namespace SixLabors.ImageSharp.Processing.Drawing
{
    /// <summary>
    /// Adds extensions that allow the filling of closed linear polygons to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class FillPolygonExtensions
    {
        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> FillPolygon<TPixel>(this IImageProcessingContext<TPixel> source, IBrush<TPixel> brush, PointF[] points, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
            => source.Fill(brush, new Polygon(new LinearLineSegment(points)), options);

        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> FillPolygon<TPixel>(this IImageProcessingContext<TPixel> source, IBrush<TPixel> brush, PointF[] points)
            where TPixel : struct, IPixel<TPixel>
            => source.Fill(brush, new Polygon(new LinearLineSegment(points)));

        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="points">The points.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> FillPolygon<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color, PointF[] points, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
            => source.Fill(new SolidBrush<TPixel>(color), new Polygon(new LinearLineSegment(points)), options);

        /// <summary>
        /// Flood fills the image in the shape of a Linear polygon described by the points
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> FillPolygon<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color, PointF[] points)
            where TPixel : struct, IPixel<TPixel>
            => source.Fill(new SolidBrush<TPixel>(color), new Polygon(new LinearLineSegment(points)));
    }
}