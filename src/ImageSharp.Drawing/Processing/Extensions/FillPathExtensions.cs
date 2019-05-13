// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;
using SixLabors.Shapes;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the filling of polygon outlines to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class FillPathExtensions
    {
        /// <summary>
        /// Flood fills the image in the shape of the provided polygon with the specified brush.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The graphics options.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="path">The shape.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Fill(
            this IImageProcessingContext source,
            GraphicsOptions options,
            IBrush brush,
            IPath path) =>
            source.Fill(options, brush, new ShapeRegion(path));

        /// <summary>
        /// Flood fills the image in the shape of the provided polygon with the specified brush.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="path">The path.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Fill(this IImageProcessingContext source, IBrush brush, IPath path) =>
            source.Fill(GraphicsOptions.Default, brush, new ShapeRegion(path));

        /// <summary>
        /// Flood fills the image in the shape of the provided polygon with the specified brush..
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="color">The color.</param>
        /// <param name="path">The path.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Fill(
            this IImageProcessingContext source,
            GraphicsOptions options,
            Color color,
            IPath path) =>
            source.Fill(options, new SolidBrush(color), path);

        /// <summary>
        /// Flood fills the image in the shape of the provided polygon with the specified brush..
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="path">The path.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Fill(this IImageProcessingContext source, Color color, IPath path) =>
            source.Fill(new SolidBrush(color), path);
    }
}