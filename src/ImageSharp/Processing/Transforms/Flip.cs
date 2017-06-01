// <copyright file="Flip.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using ImageSharp.PixelFormats;

    using ImageSharp.Processing;
    using Processing.Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Flips an image by the given instructions.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="flipType">The <see cref="FlipType"/> to perform the flip.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static Image<TPixel> Flip<TPixel>(this Image<TPixel> source, FlipType flipType)
            where TPixel : struct, IPixel<TPixel>
        {
            FlipProcessor<TPixel> processor = new FlipProcessor<TPixel>(flipType);

            source.ApplyProcessor(processor, source.Bounds);
            return source;
        }
    }
}