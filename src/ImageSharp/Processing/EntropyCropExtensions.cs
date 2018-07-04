// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the application of entropy cropping operations to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class EntropyCropExtensions
    {
        /// <summary>
        /// Crops an image to the area of greatest entropy using a threshold for entropic density of <value>.5F</value>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to crop.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> EntropyCrop<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new EntropyCropProcessor<TPixel>());

        /// <summary>
        /// Crops an image to the area of greatest entropy.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to crop.</param>
        /// <param name="threshold">The threshold for entropic density.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> EntropyCrop<TPixel>(this IImageProcessingContext<TPixel> source, float threshold)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new EntropyCropProcessor<TPixel>(threshold));
    }
}