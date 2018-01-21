// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Transforms an image by the given matrix.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(this IImageProcessingContext<TPixel> source, Matrix3x2 matrix)
            where TPixel : struct, IPixel<TPixel>
            => Transform(source, matrix, KnownResamplers.NearestNeighbor);

        /// <summary>
        /// Transforms an image by the given matrix using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(this IImageProcessingContext<TPixel> source, Matrix3x2 matrix, IResampler sampler)
            where TPixel : struct, IPixel<TPixel>
            => Transform(source, matrix, sampler, Rectangle.Empty);

        /// <summary>
        /// Transforms an image by the given matrix using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="rectangle">The rectangle to constrain the transformed image to.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(
            this IImageProcessingContext<TPixel> source,
            Matrix3x2 matrix,
            IResampler sampler,
            Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            // TODO: Fixme!
            return source.ApplyProcessor(new AffineTransformProcessor<TPixel>(matrix, sampler, rectangle.Size));
        }

        /// <summary>
        /// Transforms an image by the given matrix using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="destinationSize">The dimensions to constrain the transformed image to.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Transform<TPixel>(
            this IImageProcessingContext<TPixel> source,
            Matrix3x2 matrix,
            IResampler sampler,
            Size destinationSize)
            where TPixel : struct, IPixel<TPixel>
        {
            return source.ApplyProcessor(new AffineTransformProcessor<TPixel>(matrix, sampler, destinationSize));
        }

        /// <summary>
        /// Transforms an image by the given matrix.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        internal static IImageProcessingContext<TPixel> Transform<TPixel>(this IImageProcessingContext<TPixel> source, Matrix4x4 matrix)
            where TPixel : struct, IPixel<TPixel>
            => Transform(source, matrix, KnownResamplers.NearestNeighbor);

        /// <summary>
        /// Transforms an image by the given matrix using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        internal static IImageProcessingContext<TPixel> Transform<TPixel>(this IImageProcessingContext<TPixel> source, Matrix4x4 matrix, IResampler sampler)
            where TPixel : struct, IPixel<TPixel>
            => Transform(source, matrix, sampler, Rectangle.Empty);

        /// <summary>
        /// Transforms an image by the given matrix using the specified sampling algorithm.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to transform.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="rectangle">The rectangle to constrain the transformed image to.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        internal static IImageProcessingContext<TPixel> Transform<TPixel>(this IImageProcessingContext<TPixel> source, Matrix4x4 matrix, IResampler sampler, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new NonAffineTransformProcessor<TPixel>(matrix, sampler, rectangle));
    }
}