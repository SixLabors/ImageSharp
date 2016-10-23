// <copyright file="OilPainting.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;

    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor, TPacked}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Alters the colors of the image recreating an oil painting effect.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="levels">The number of intensity levels. Higher values result in a broader range of colour intensities forming part of the result image.</param>
        /// <param name="brushSize">The number of neighbouring pixels used in calculating each individual pixel value.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> OilPaint<TColor, TPacked>(this Image<TColor, TPacked> source, int levels = 10, int brushSize = 15)
            where TColor : IPackedPixel<TPacked>
            where TPacked : struct
        {
            return OilPaint(source, levels, brushSize, source.Bounds);
        }

        /// <summary>
        /// Alters the colors of the image recreating an oil painting effect.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="levels">The number of intensity levels. Higher values result in a broader range of colour intensities forming part of the result image.</param>
        /// <param name="brushSize">The number of neighbouring pixels used in calculating each individual pixel value.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> OilPaint<TColor, TPacked>(this Image<TColor, TPacked> source, int levels, int brushSize, Rectangle rectangle)
            where TColor : IPackedPixel<TPacked>
            where TPacked : struct
        {
            Guard.MustBeGreaterThan(levels, 0, nameof(levels));

            if (brushSize <= 0 || brushSize > source.Height || brushSize > source.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(brushSize));
            }

            return source.Process(rectangle, new OilPaintingProcessor<TColor, TPacked>(levels, brushSize));
        }
    }
}