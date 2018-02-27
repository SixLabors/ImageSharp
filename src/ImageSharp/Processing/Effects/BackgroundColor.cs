﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Replaces the background color of image with the given one.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the background.</param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BackgroundColor<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        => source.ApplyProcessor(new BackgroundColorProcessor<TPixel>(source.MemoryManager, color, options));

        /// <summary>
        /// Replaces the background color of image with the given one.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the background.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="options">The options effecting pixel blending.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BackgroundColor<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color, Rectangle rectangle, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        => source.ApplyProcessor(new BackgroundColorProcessor<TPixel>(source.MemoryManager, color, options), rectangle);

        /// <summary>
        /// Replaces the background color of image with the given one.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the background.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BackgroundColor<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color)
            where TPixel : struct, IPixel<TPixel>
        {
            return BackgroundColor(source, color, GraphicsOptions.Default);
        }

        /// <summary>
        /// Replaces the background color of image with the given one.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the background.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> BackgroundColor<TPixel>(this IImageProcessingContext<TPixel> source, TPixel color, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            return BackgroundColor(source, color, rectangle, GraphicsOptions.Default);
        }
    }
}
