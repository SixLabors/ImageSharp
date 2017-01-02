// <copyright file="BinaryThreshold.cs" company="James Jackson-South">
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
        /// Applies binarization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> BinaryThreshold<TColor>(this Image<TColor> source, float threshold)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return BinaryThreshold(source, threshold, source.Bounds);
        }

        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> BinaryThreshold<TColor>(this Image<TColor> source, float threshold, Rectangle rectangle)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Apply(rectangle, new BinaryThresholdProcessor<TColor>(threshold));
        }
    }
}
