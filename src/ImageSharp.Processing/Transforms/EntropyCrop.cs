// <copyright file="EntropyCrop.cs" company="James Jackson-South">
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
        /// Crops an image to the area of greatest entropy.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image to crop.</param>
        /// <param name="threshold">The threshold for entropic density.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor> EntropyCrop<TColor>(this Image<TColor> source, float threshold = .5f)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            EntropyCropProcessor<TColor> processor = new EntropyCropProcessor<TColor>(threshold);
            return source.Apply(source.Bounds, processor);
        }
    }
}