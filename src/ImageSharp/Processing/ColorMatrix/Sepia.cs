// <copyright file="Sepia.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using ImageSharp.PixelFormats;

    using ImageSharp.Processing;
    using Processing.Processors;
    using SixLabors.Primitives;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies sepia toning to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Sepia<TPixel>(this Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
        {
            return Sepia(source, source.Bounds);
        }

        /// <summary>
        /// Applies sepia toning to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Sepia<TPixel>(this Image<TPixel> source, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            source.ApplyProcessor(new SepiaProcessor<TPixel>(), rectangle);
            return source;
        }
    }
}