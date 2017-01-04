// <copyright file="Flip.cs" company="James Jackson-South">
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
        /// Flips an image by the given instructions.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="flipType">The <see cref="FlipType"/> to perform the flip.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor> Flip<TColor>(this Image<TColor> source, FlipType flipType)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            FlipProcessor<TColor> processor = new FlipProcessor<TColor>(flipType);
            return source.Apply(source.Bounds, processor);
        }
    }
}