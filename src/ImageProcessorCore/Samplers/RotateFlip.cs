// <copyright file="RotateFlip.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TColor, TPacked}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Rotates and flips an image by the given instructions.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="flipType">The <see cref="FlipType"/> to perform the flip.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor, TPacked> RotateFlip<TColor, TPacked>(this Image<TColor, TPacked> source, RotateType rotateType, FlipType flipType)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return source.Rotate(rotateType).Flip(flipType);
        }
    }
}
