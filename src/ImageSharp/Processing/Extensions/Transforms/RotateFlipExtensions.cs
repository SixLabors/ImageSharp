// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the application of rotate-flip operations on an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class RotateFlipExtensions
    {
        /// <summary>
        /// Rotates and flips an image by the given instructions.
        /// </summary>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="rotateMode">The <see cref="RotateMode"/> to perform the rotation.</param>
        /// <param name="flipMode">The <see cref="FlipMode"/> to perform the flip.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext RotateFlip(this IImageProcessingContext source, RotateMode rotateMode, FlipMode flipMode)
            => source.Rotate(rotateMode).Flip(flipMode);
    }
}