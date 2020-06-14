// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the application of flipping operations on an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class FlipExtensions
    {
        /// <summary>
        /// Flips an image by the given instructions.
        /// </summary>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="flipMode">The <see cref="FlipMode"/> to perform the flip.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Flip(this IImageProcessingContext source, FlipMode flipMode)
            => source.ApplyProcessor(new FlipProcessor(flipMode));
    }
}