// <copyright file="Crop.cs" company="James Jackson-South">
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
        /// Crops an image to the given width and height.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static Image<TPixel> Crop<TPixel>(this Image<TPixel> source, int width, int height)
            where TPixel : struct, IPixel<TPixel>
        {
            return Crop(source, new Rectangle(0, 0, width, height));
        }

        /// <summary>
        /// Crops an image to the given rectangle.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to crop.</param>
        /// <param name="cropRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to retain.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static Image<TPixel> Crop<TPixel>(this Image<TPixel> source, Rectangle cropRectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            CropProcessor<TPixel> processor = new CropProcessor<TPixel>(cropRectangle);

            source.ApplyProcessor(processor, source.Bounds);
            return source;
        }
    }
}