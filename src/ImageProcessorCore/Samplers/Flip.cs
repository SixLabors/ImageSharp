// <copyright file="Flip.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor, TPacked}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Flips an image by the given instructions.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="flipType">The <see cref="FlipType"/> to perform the flip.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor, TPacked> Flip<TColor, TPacked>(this Image<TColor, TPacked> source, FlipType flipType)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            FlipProcessor<TColor, TPacked> processor = new FlipProcessor<TColor, TPacked>(flipType);
            return source.Process(source.Width, source.Height, source.Bounds, source.Bounds, processor);
        }
    }
}