// <copyright file="Crop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using Processing.Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Crops an image to the given width and height.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <returns>The <see cref="Image{TColor}"/></returns>
        public static Image<TColor> Crop<TColor>(this Image<TColor> source, int width, int height)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return Crop(source, new Rectangle(0, 0, width, height));
        }

        /// <summary>
        /// Crops an image to the given rectangle.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image to crop.</param>
        /// <param name="cropRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to retain.
        /// </param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor> Crop<TColor>(this Image<TColor> source, Rectangle cropRectangle)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            CropProcessor<TColor> processor = new CropProcessor<TColor>(cropRectangle);
            return source.Apply(source.Bounds, processor);
        }
    }
}