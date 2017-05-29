// <copyright file="RotateFlip.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using ImageSharp.PixelFormats;

    using ImageSharp.Processing;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Rotates and flips an image by the given instructions.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="rotateType">The <see cref="RotateType"/> to perform the rotation.</param>
        /// <param name="flipType">The <see cref="FlipType"/> to perform the flip.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static Image<TPixel> RotateFlip<TPixel>(this Image<TPixel> source, RotateType rotateType, FlipType flipType)
            where TPixel : struct, IPixel<TPixel>
        {
            return source.Rotate(rotateType).Flip(flipType);
        }
    }
}
