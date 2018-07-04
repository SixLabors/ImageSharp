// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using SixLabors.Shapes;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the drawing of lines to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class DrawLineExtensions
    {
        /// <summary>
        /// Draws the provided Points as an open Linear path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DrawLines<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, IBrush<TPixel> brush, float thickness, params PointF[] points)
            where TPixel : struct, IPixel<TPixel>
            => source.Draw(options, new Pen<TPixel>(brush, thickness), new Path(new LinearLineSegment(points)));

        /// <summary>
        /// Draws the provided Points as an open Linear path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DrawLines<TPixel>(this IImageProcessingContext<TPixel> source, IBrush<TPixel> brush, float thickness, params PointF[] points)
            where TPixel : struct, IPixel<TPixel>
            => source.Draw(new Pen<TPixel>(brush, thickness), new Path(new LinearLineSegment(points)));

        /// <summary>
        /// Draws the provided Points as an open Linear path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DrawLines<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color, float thickness, params PointF[] points)
            where TPixel : struct, IPixel<TPixel>
            => source.DrawLines(new SolidBrush<TPixel>(color), thickness, points);

        /// <summary>
        /// Draws the provided Points as an open Linear path at the provided thickness with the supplied brush
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>>
        public static IImageProcessingContext<TPixel> DrawLines<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, TPixel color, float thickness, params PointF[] points)
            where TPixel : struct, IPixel<TPixel>
            => source.DrawLines(options, new SolidBrush<TPixel>(color), thickness, points);

        /// <summary>
        /// Draws the provided Points as an open Linear path with the supplied pen
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DrawLines<TPixel>(this IImageProcessingContext<TPixel> source, GraphicsOptions options, IPen<TPixel> pen, params PointF[] points)
            where TPixel : struct, IPixel<TPixel>
            => source.Draw(options, pen, new Path(new LinearLineSegment(points)));

        /// <summary>
        /// Draws the provided Points as an open Linear path with the supplied pen
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DrawLines<TPixel>(this IImageProcessingContext<TPixel> source, IPen<TPixel> pen, params PointF[] points)
            where TPixel : struct, IPixel<TPixel>
            => source.Draw(pen, new Path(new LinearLineSegment(points)));
    }
}