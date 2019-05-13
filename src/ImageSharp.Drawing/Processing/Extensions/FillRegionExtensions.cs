// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing.Processors.Drawing;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the filling of regions with various brushes to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class FillRegionExtensions
    {
        /// <summary>
        /// Flood fills the image with the specified brush.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The details how to fill the region of interest.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Fill(this IImageProcessingContext source, IBrush brush) =>
            source.Fill(GraphicsOptions.Default, brush);

        /// <summary>
        /// Flood fills the image with the specified color.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Fill(this IImageProcessingContext source, Color color) =>
            source.Fill(new SolidBrush(color));

        /// <summary>
        /// Flood fills the image with in the region with the specified brush.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="region">The region.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Fill(this IImageProcessingContext source, IBrush brush, Region region) =>
            source.Fill(GraphicsOptions.Default, brush, region);

        /// <summary>
        /// Flood fills the image with in the region with the specified color.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="color">The color.</param>
        /// <param name="region">The region.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Fill(
            this IImageProcessingContext source,
            GraphicsOptions options,
            Color color,
            Region region) =>
            source.Fill(options, new SolidBrush(color), region);

        /// <summary>
        /// Flood fills the image with in the region with the specified color.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="region">The region.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Fill(this IImageProcessingContext source, Color color, Region region) =>
            source.Fill(new SolidBrush(color), region);

        /// <summary>
        /// Flood fills the image with in the region with the specified brush.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The graphics options.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="region">The region.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Fill(
            this IImageProcessingContext source,
            GraphicsOptions options,
            IBrush brush,
            Region region) =>
            source.ApplyProcessor(new FillRegionProcessor(brush, region, options));

        /// <summary>
        /// Flood fills the image with the specified brush.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The graphics options.</param>
        /// <param name="brush">The details how to fill the region of interest.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext Fill(
            this IImageProcessingContext source,
            GraphicsOptions options,
            IBrush brush) =>
            source.ApplyProcessor(new FillProcessor(brush, options));
    }
}