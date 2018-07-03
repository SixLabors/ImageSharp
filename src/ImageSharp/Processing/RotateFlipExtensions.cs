// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the application of rotate-flip operations to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class RotateFlipExtensions
    {
        /// <summary>
        /// Rotates and flips an image by the given instructions.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="rotateMode">The <see cref="RotateMode"/> to perform the rotation.</param>
        /// <param name="flipMode">The <see cref="FlipMode"/> to perform the flip.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> RotateFlip<TPixel>(this IImageProcessingContext<TPixel> source, RotateMode rotateMode, FlipMode flipMode)
            where TPixel : struct, IPixel<TPixel>
            => source.Rotate(rotateMode).Flip(flipMode);
    }
}