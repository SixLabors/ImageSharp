// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the application of rotate operations to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class RotateExtensions
    {
        /// <summary>
        /// Rotates and flips an image by the given instructions.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate.</param>
        /// <param name="rotateMode">The <see cref="RotateMode"/> to perform the rotation.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Rotate<TPixel>(this IImageProcessingContext<TPixel> source, RotateMode rotateMode)
            where TPixel : struct, IPixel<TPixel>
            => Rotate(source, (float)rotateMode);

        /// <summary>
        /// Rotates an image by the given angle in degrees.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate.</param>
        /// <param name="degrees">The angle in degrees to perform the rotation.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Rotate<TPixel>(this IImageProcessingContext<TPixel> source, float degrees)
            where TPixel : struct, IPixel<TPixel>
            => Rotate(source, degrees, KnownResamplers.Bicubic);

        /// <summary>
        /// Rotates an image by the given angle in degrees using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate.</param>
        /// <param name="degrees">The angle in degrees to perform the rotation.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Rotate<TPixel>(this IImageProcessingContext<TPixel> source, float degrees, IResampler sampler)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new RotateProcessor<TPixel>(degrees, sampler, source.GetCurrentSize()));
    }
}