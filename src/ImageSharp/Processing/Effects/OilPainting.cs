// <copyright file="OilPainting.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using ImageSharp.PixelFormats;

    using Processing.Processors;
    using SixLabors.Primitives;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Alters the colors of the image recreating an oil painting effect with levels and brushSize
        /// set to <value>10</value> and <value>15</value> respectively.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> OilPaint<TPixel>(this Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
        {
            return OilPaint(source, 10, 15);
        }

        /// <summary>
        /// Alters the colors of the image recreating an oil painting effect  with levels and brushSize
        /// set to <value>10</value> and <value>15</value> respectively.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> OilPaint<TPixel>(this Image<TPixel> source, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            return OilPaint(source, 10, 15, rectangle);
        }

        /// <summary>
        /// Alters the colors of the image recreating an oil painting effect.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="levels">The number of intensity levels. Higher values result in a broader range of color intensities forming part of the result image.</param>
        /// <param name="brushSize">The number of neighboring pixels used in calculating each individual pixel value.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> OilPaint<TPixel>(this Image<TPixel> source, int levels, int brushSize)
        where TPixel : struct, IPixel<TPixel>
        {
            return OilPaint(source, levels, brushSize, source.Bounds);
        }

        /// <summary>
        /// Alters the colors of the image recreating an oil painting effect.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="levels">The number of intensity levels. Higher values result in a broader range of color intensities forming part of the result image.</param>
        /// <param name="brushSize">The number of neighboring pixels used in calculating each individual pixel value.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> OilPaint<TPixel>(this Image<TPixel> source, int levels, int brushSize, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.MustBeGreaterThan(levels, 0, nameof(levels));

            if (brushSize <= 0 || brushSize > source.Height || brushSize > source.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(brushSize));
            }

            source.ApplyProcessor(new OilPaintingProcessor<TPixel>(levels, brushSize), rectangle);
            return source;
        }
    }
}