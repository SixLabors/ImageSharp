// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Brushes;
using SixLabors.ImageSharp.Drawing.Processors;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
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
        {
            return source.ApplyProcessor(new FillProcessor<TPixel>(brush, options));
        }

        /// <summary>
        /// Flood fills the image with the specified brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The details how to fill the region of interest.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Fill<TPixel>(this IImageProcessingContext<TPixel> source, IBrush<TPixel> brush)
            where TPixel : struct, IPixel<TPixel>
        {
            return source.Fill(brush, GraphicsOptions.Default);
        }

        /// <summary>
        /// Flood fills the image with the specified color.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Fill<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color)
            where TPixel : struct, IPixel<TPixel>
        {
            return source.Fill(new SolidBrush<TPixel>(color));
        }

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
        {
            return source.ApplyProcessor(new FillRegionProcessor<TPixel>(brush, region, options));
        }

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
        {
            return source.Fill(brush, region, GraphicsOptions.Default);
        }

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
        {
            return source.Fill(new SolidBrush<TPixel>(color), region, options);
        }

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
        {
            return source.Fill(new SolidBrush<TPixel>(color), region);
        }
    }
}
