// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Brushes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using SixLabors.Shapes;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
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
        {
            return source.Fill(brush, new Polygon(new LinearLineSegment(points)), options);
        }

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
        {
            return source.Fill(brush, new Polygon(new LinearLineSegment(points)));
        }

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
        {
            return source.Fill(new SolidBrush<TPixel>(color), new Polygon(new LinearLineSegment(points)), options);
        }

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
        {
            return source.Fill(new SolidBrush<TPixel>(color), new Polygon(new LinearLineSegment(points)));
        }
    }
}
