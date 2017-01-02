// <copyright file="Pixelate.cs" company="James Jackson-South">
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
        /// Pixelates an image with the given pixel size.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="size">The size of the pixels.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Pixelate<TColor>(this Image<TColor> source, int size = 4)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return Pixelate(source, size, source.Bounds);
        }

        /// <summary>
        /// Pixelates an image with the given pixel size.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="size">The size of the pixels.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Pixelate<TColor>(this Image<TColor> source, int size, Rectangle rectangle)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            if (size <= 0 || size > source.Height || size > source.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            return source.Apply(rectangle, new PixelateProcessor<TColor>(size));
        }
    }
}