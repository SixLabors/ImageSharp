// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the application of flipping operations to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class FlipExtensions
    {
        /// <summary>
        /// Flips an image by the given instructions.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="flipMode">The <see cref="FlipMode"/> to perform the flip.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Flip<TPixel>(this IImageProcessingContext<TPixel> source, FlipMode flipMode)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new FlipProcessor<TPixel>(flipMode));
    }
}