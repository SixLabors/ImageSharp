// <copyright file="BoxBlur.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using Processing;
    using Processing.Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies a box blur to the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> BoxBlur<TColor>(this Image<TColor> source, int radius = 7)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return BoxBlur(source, radius, source.Bounds);
        }

        /// <summary>
        /// Applies a box blur to the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> BoxBlur<TColor>(this Image<TColor> source, int radius, Rectangle rectangle)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Apply(rectangle, new BoxBlurProcessor<TColor>(radius));
        }
    }
}
