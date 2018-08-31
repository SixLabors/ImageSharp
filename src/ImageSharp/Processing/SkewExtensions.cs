// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the application of skew operations to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class SkewExtensions
    {
        /// <summary>
        /// Skews an image by the given angles in degrees.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to skew.</param>
        /// <param name="degreesX">The angle in degrees to perform the skew along the x-axis.</param>
        /// <param name="degreesY">The angle in degrees to perform the skew along the y-axis.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Skew<TPixel>(this IImageProcessingContext<TPixel> source, float degreesX, float degreesY)
            where TPixel : struct, IPixel<TPixel>
            => Skew(source, degreesX, degreesY, KnownResamplers.Bicubic);

        /// <summary>
        /// Skews an image by the given angles in degrees using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to skew.</param>
        /// <param name="degreesX">The angle in degrees to perform the skew along the x-axis.</param>
        /// <param name="degreesY">The angle in degrees to perform the skew along the y-axis.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Skew<TPixel>(this IImageProcessingContext<TPixel> source, float degreesX, float degreesY, IResampler sampler)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new SkewProcessor<TPixel>(degreesX, degreesY, sampler, source.GetCurrentSize()));
    }
}