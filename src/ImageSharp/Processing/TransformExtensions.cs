// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.Primitives;

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
        public static IImageProcessingContext<TPixel> Transform<TPixel>(
            this IImageProcessingContext<TPixel> source,
            AffineTransformBuilder builder)
            where TPixel : struct, IPixel<TPixel>
            => Transform(source, builder, KnownResamplers.Bicubic);

        /// <summary>
        /// Performs an affine transform of an image using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="ctx">The <see cref="IImageProcessingContext{TPixel}"/>.</param>
        /// <param name="builder">The affine transform builder.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(
            this IImageProcessingContext<TPixel> ctx,
            AffineTransformBuilder builder,
            IResampler sampler)
            where TPixel : struct, IPixel<TPixel>
            => ctx.Transform(new Rectangle(Point.Empty, ctx.GetCurrentSize()), builder, sampler);

        /// <summary>
        /// Performs an affine transform of an image using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="ctx">The <see cref="IImageProcessingContext{TPixel}"/>.</param>
        /// <param name="sourceRectangle">The source rectangle</param>
        /// <param name="builder">The affine transform builder.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(
            this IImageProcessingContext<TPixel> ctx,
            Rectangle sourceRectangle,
            AffineTransformBuilder builder,
            IResampler sampler)
            where TPixel : struct, IPixel<TPixel>
        {
            Matrix3x2 transform = builder.BuildMatrix(sourceRectangle);
            Size targetDimensions = TransformUtils.GetTransformedSize(sourceRectangle.Size, transform);
            return ctx.Transform(sourceRectangle, transform, targetDimensions, sampler);
        }

        /// <summary>
        /// Performs an affine transform of an image using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="ctx">The <see cref="IImageProcessingContext{TPixel}"/>.</param>
        /// <param name="sourceRectangle">The source rectangle</param>
        /// <param name="transform">The transformation matrix.</param>
        /// <param name="targetDimensions">The size of the result image.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(
            this IImageProcessingContext<TPixel> ctx,
            Rectangle sourceRectangle,
            Matrix3x2 transform,
            Size targetDimensions,
            IResampler sampler)
            where TPixel : struct, IPixel<TPixel>
        {
            return ctx.ApplyProcessor(
                new AffineTransformProcessor<TPixel>(transform, sampler, targetDimensions),
                sourceRectangle);
        }

        /// <summary>
        /// Performs a projective transform of an image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="builder">The affine transform builder.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(
            this IImageProcessingContext<TPixel> source,
            ProjectiveTransformBuilder builder)
            where TPixel : struct, IPixel<TPixel>
            => Transform(source, builder, KnownResamplers.Bicubic);

        /// <summary>
        /// Performs a projective transform of an image using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="ctx">The <see cref="IImageProcessingContext{TPixel}"/>.</param>
        /// <param name="builder">The projective transform builder.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(
            this IImageProcessingContext<TPixel> ctx,
            ProjectiveTransformBuilder builder,
            IResampler sampler)
            where TPixel : struct, IPixel<TPixel>
            => ctx.Transform(new Rectangle(Point.Empty, ctx.GetCurrentSize()), builder, sampler);

        /// <summary>
        /// Performs a projective transform of an image using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="ctx">The <see cref="IImageProcessingContext{TPixel}"/>.</param>
        /// <param name="sourceRectangle">The source rectangle</param>
        /// <param name="builder">The projective transform builder.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(
            this IImageProcessingContext<TPixel> ctx,
            Rectangle sourceRectangle,
            ProjectiveTransformBuilder builder,
            IResampler sampler)
            where TPixel : struct, IPixel<TPixel>
        {
            Matrix4x4 transform = builder.BuildMatrix(sourceRectangle);
            Size targetDimensions = TransformUtils.GetTransformedSize(sourceRectangle.Size, transform);
            return ctx.Transform(sourceRectangle, transform, targetDimensions, sampler);
        }

        /// <summary>
        /// Performs a projective transform of an image using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="ctx">The <see cref="IImageProcessingContext{TPixel}"/>.</param>
        /// <param name="sourceRectangle">The source rectangle</param>
        /// <param name="transform">The transformation matrix.</param>
        /// <param name="targetDimensions">The size of the result image.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(
            this IImageProcessingContext<TPixel> ctx,
            Rectangle sourceRectangle,
            Matrix4x4 transform,
            Size targetDimensions,
            IResampler sampler)
            where TPixel : struct, IPixel<TPixel>
        {
            return ctx.ApplyProcessor(
                new ProjectiveTransformProcessor<TPixel>(transform, sampler, targetDimensions),
                sourceRectangle);
        }
    }
}