// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Drawing.Brushes;
using SixLabors.ImageSharp.Drawing.Processors;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Overlays
{
    /// <summary>
    /// Adds extensions that allow the filling of regions with various brushes to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class FillRegionExtensions
    {
        /// <summary>
        /// Flood fills the image with the specified brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The details how to fill the region of interest.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Fill<TPixel>(this IImageProcessingContext<TPixel> source, IBrush<TPixel> brush)
            where TPixel : struct, IPixel<TPixel>
            => source.Fill(brush, GraphicsOptions.Default);

        /// <summary>
        /// Flood fills the image with the specified color.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Fill<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color)
            where TPixel : struct, IPixel<TPixel>
            => source.Fill(new SolidBrush<TPixel>(color));

        /// <summary>
        /// Flood fills the image with in the region with the specified brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="region">The region.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Fill<TPixel>(this IImageProcessingContext<TPixel> source, IBrush<TPixel> brush, Region region)
            where TPixel : struct, IPixel<TPixel>
            => source.Fill(brush, region, GraphicsOptions.Default);

        /// <summary>
        /// Flood fills the image with in the region with the specified color.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="region">The region.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Fill<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color, Region region, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
            => source.Fill(new SolidBrush<TPixel>(color), region, options);

        /// <summary>
        /// Flood fills the image with in the region with the specified color.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="region">The region.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Fill<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color, Region region)
            where TPixel : struct, IPixel<TPixel>
            => source.Fill(new SolidBrush<TPixel>(color), region);

        /// <summary>
        /// Flood fills the image with in the region with the specified brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="region">The region.</param>
        /// <param name="options">The graphics options.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Fill<TPixel>(this IImageProcessingContext<TPixel> source, IBrush<TPixel> brush, Region region, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new FillRegionProcessor<TPixel>(brush, region, options));

        /// <summary>
        /// Flood fills the image with the specified brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The details how to fill the region of interest.</param>
        /// <param name="options">The graphics options.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Fill<TPixel>(this IImageProcessingContext<TPixel> source, IBrush<TPixel> brush, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new FillProcessor<TPixel>(brush, options));
    }
}