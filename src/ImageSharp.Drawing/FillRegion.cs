// <copyright file="FillRegion.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;
    using Drawing;
    using Drawing.Brushes;
    using Drawing.Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Flood fills the image with the specified brush.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The details how to fill the region of interest.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, IBrush<TColor> brush)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Apply(new FillProcessor<TColor>(brush));
        }

        /// <summary>
        /// Flood fills the image with the specified color.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, TColor color)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Fill(new SolidBrush<TColor>(color));
        }

        /// <summary>
        /// Flood fills the image with in the region with the specified brush.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="region">The region.</param>
        /// <param name="options">The graphics options.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, IBrush<TColor> brush, Region region, GraphicsOptions options)
          where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Apply(new FillRegionProcessor<TColor>(brush, region, options));
        }

        /// <summary>
        /// Flood fills the image with in the region with the specified brush.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="region">The region.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, IBrush<TColor> brush, Region region)
          where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Fill(brush, region, GraphicsOptions.Default);
        }

        /// <summary>
        /// Flood fills the image with in the region with the specified color.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="region">The region.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, TColor color, Region region, GraphicsOptions options)
          where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Fill(new SolidBrush<TColor>(color), region, options);
        }

        /// <summary>
        /// Flood fills the image with in the region with the specified color.
        /// </summary>
        /// <typeparam name="TColor">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="region">The region.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Fill<TColor>(this Image<TColor> source, TColor color, Region region)
          where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Fill(new SolidBrush<TColor>(color), region);
        }
    }
}
