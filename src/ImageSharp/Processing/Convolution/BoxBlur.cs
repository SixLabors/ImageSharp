// <copyright file="BoxBlur.cs" company="James Jackson-South">
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
        /// Applies a box blur to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageOperations<TPixel> BoxBlur<TPixel>(this IImageOperations<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BoxBlurProcessor<TPixel>(7));

        /// <summary>
        /// Applies a box blur to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageOperations<TPixel> BoxBlur<TPixel>(this IImageOperations<TPixel> source, int radius)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BoxBlurProcessor<TPixel>(radius));

        /// <summary>
        /// Applies a box blur to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageOperations<TPixel> BoxBlur<TPixel>(this IImageOperations<TPixel> source, int radius, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new BoxBlurProcessor<TPixel>(radius), rectangle);
    }
}
