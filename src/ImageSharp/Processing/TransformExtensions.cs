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
        /// Transforms an image by the given matrix.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="builder">The affine transform builder.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(this IImageProcessingContext<TPixel> source, AffineTransformBuilder builder)
            where TPixel : struct, IPixel<TPixel>
            => Transform(source, builder, KnownResamplers.Bicubic);

        /// <summary>
        /// Transforms an image by the given matrix using the specified sampling algorithm.
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
        /// Transforms an image by the given matrix.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(this IImageProcessingContext<TPixel> source, Matrix4x4 matrix)
            where TPixel : struct, IPixel<TPixel>
            => Transform(source, matrix, KnownResamplers.Bicubic);

        /// <summary>
        /// Applies a projective transform to the image by the given matrix using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(this IImageProcessingContext<TPixel> source, Matrix4x4 matrix, IResampler sampler)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new ProjectiveTransformProcessor<TPixel>(matrix, sampler, source.GetCurrentSize()));

        /// <summary>
        /// Applies a projective transform to the image by the given matrix using the specified sampling algorithm.
        /// TODO: Should we be offsetting the matrix here?
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="rectangle">The rectangle to constrain the transformed image to.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        internal static IImageProcessingContext<TPixel> Transform<TPixel>(this IImageProcessingContext<TPixel> source, Matrix4x4 matrix, IResampler sampler, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            var t = Matrix4x4.CreateTranslation(new Vector3(-rectangle.Location, 0));
            Matrix4x4 combinedMatrix = t * matrix;
            return source.ApplyProcessor(new ProjectiveTransformProcessor<TPixel>(combinedMatrix, sampler, rectangle.Size));
        }
    }
}