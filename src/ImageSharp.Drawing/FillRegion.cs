// <copyright file="FillRegion.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using Drawing;
    using Drawing.Brushes;
    using Drawing.Processors;
    using ImageSharp.PixelFormats;

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
        public static Image<TPixel> Fill<TPixel>(this Image<TPixel> source, IBrush<TPixel> brush, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            return source.Apply(new FillProcessor<TPixel>(brush, options));
        }

        /// <summary>
        /// Flood fills the image with the specified brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The details how to fill the region of interest.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Fill<TPixel>(this Image<TPixel> source, IBrush<TPixel> brush)
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
        public static Image<TPixel> Fill<TPixel>(this Image<TPixel> source, TPixel color)
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
        public static Image<TPixel> Fill<TPixel>(this Image<TPixel> source, IBrush<TPixel> brush, Region region, GraphicsOptions options)
          where TPixel : struct, IPixel<TPixel>
        {
            return source.Apply(new FillRegionProcessor<TPixel>(brush, region, options));
        }

        /// <summary>
        /// Flood fills the image with in the region with the specified brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="region">The region.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Fill<TPixel>(this Image<TPixel> source, IBrush<TPixel> brush, Region region)
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
        public static Image<TPixel> Fill<TPixel>(this Image<TPixel> source, TPixel color, Region region, GraphicsOptions options)
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
        public static Image<TPixel> Fill<TPixel>(this Image<TPixel> source, TPixel color, Region region)
          where TPixel : struct, IPixel<TPixel>
        {
            return source.Fill(new SolidBrush<TPixel>(color), region);
        }
    }
}
