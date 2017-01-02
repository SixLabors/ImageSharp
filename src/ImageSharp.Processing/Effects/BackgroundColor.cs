// <copyright file="BackgroundColor.cs" company="James Jackson-South">
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
        /// Replaces the background color of image with the given one.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the background.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> BackgroundColor<TColor>(this Image<TColor> source, TColor color)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Apply(source.Bounds, new BackgroundColorProcessor<TColor>(color));
        }
    }
}
