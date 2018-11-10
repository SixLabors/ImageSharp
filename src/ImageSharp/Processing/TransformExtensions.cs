// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the application of composable transform operations to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class TransformExtensions
    {
        /// <summary>
        /// Performs an affine transform of an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="builder">The affine transform builder.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(this IImageProcessingContext<TPixel> source, AffineTransformBuilder builder)
            where TPixel : struct, IPixel<TPixel>
            => Transform(source, builder, KnownResamplers.Bicubic);

        /// <summary>
        /// Performs an affine transform of an image using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="builder">The affine transform builder.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(this IImageProcessingContext<TPixel> source, AffineTransformBuilder builder, IResampler sampler)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new AffineTransformProcessor<TPixel>(builder.BuildMatrix(), sampler, builder.Size));

        /// <summary>
        /// Performs a projective transform of an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="builder">The affine transform builder.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(this IImageProcessingContext<TPixel> source, ProjectiveTransformBuilder builder)
            where TPixel : struct, IPixel<TPixel>
            => Transform(source, builder, KnownResamplers.Bicubic);

        /// <summary>
        /// Performs a projective transform of an image using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="builder">The projective transform builder.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(this IImageProcessingContext<TPixel> source, ProjectiveTransformBuilder builder, IResampler sampler)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new ProjectiveTransformProcessor<TPixel>(builder.BuildMatrix(), sampler, builder.Size));
    }
}