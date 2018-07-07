// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the application of resize operations to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class ResizeExtensions
    {
        /// <summary>
        /// Resizes an image in accordance with the given <see cref="ResizeOptions"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="options">The resize options.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        /// <remarks>Passing zero for one of height or width within the resize options will automatically preserve the aspect ratio of the original image</remarks>
        public static IImageProcessingContext<TPixel> Resize<TPixel>(this IImageProcessingContext<TPixel> source, ResizeOptions options)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new ResizeProcessor<TPixel>(options, source.GetCurrentSize()));

        /// <summary>
        /// Resizes an image to the given <see cref="Size"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="size">The target image size.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static IImageProcessingContext<TPixel> Resize<TPixel>(this IImageProcessingContext<TPixel> source, Size size)
            where TPixel : struct, IPixel<TPixel>
            => Resize(source, size.Width, size.Height, KnownResamplers.Bicubic, false);

        /// <summary>
        /// Resizes an image to the given <see cref="Size"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="size">The target image size.</param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static IImageProcessingContext<TPixel> Resize<TPixel>(this IImageProcessingContext<TPixel> source, Size size, bool compand)
            where TPixel : struct, IPixel<TPixel>
            => Resize(source, size.Width, size.Height, KnownResamplers.Bicubic, compand);

        /// <summary>
        /// Resizes an image to the given width and height.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static IImageProcessingContext<TPixel> Resize<TPixel>(this IImageProcessingContext<TPixel> source, int width, int height)
            where TPixel : struct, IPixel<TPixel>
            => Resize(source, width, height, KnownResamplers.Bicubic, false);

        /// <summary>
        /// Resizes an image to the given width and height.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static IImageProcessingContext<TPixel> Resize<TPixel>(this IImageProcessingContext<TPixel> source, int width, int height, bool compand)
            where TPixel : struct, IPixel<TPixel>
            => Resize(source, width, height, KnownResamplers.Bicubic, compand);

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static IImageProcessingContext<TPixel> Resize<TPixel>(this IImageProcessingContext<TPixel> source, int width, int height, IResampler sampler)
            where TPixel : struct, IPixel<TPixel>
            => Resize(source, width, height, sampler, false);

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="size">The target image size.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static IImageProcessingContext<TPixel> Resize<TPixel>(this IImageProcessingContext<TPixel> source, Size size, IResampler sampler, bool compand)
            where TPixel : struct, IPixel<TPixel>
            => Resize(source, size.Width, size.Height, sampler, new Rectangle(0, 0, size.Width, size.Height), compand);

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static IImageProcessingContext<TPixel> Resize<TPixel>(this IImageProcessingContext<TPixel> source, int width, int height, IResampler sampler, bool compand)
            where TPixel : struct, IPixel<TPixel>
            => Resize(source, width, height, sampler, new Rectangle(0, 0, width, height), compand);

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler and
        /// source rectangle.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="targetRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the target image object to draw to.
        /// </param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static IImageProcessingContext<TPixel> Resize<TPixel>(
            this IImageProcessingContext<TPixel> source,
            int width,
            int height,
            IResampler sampler,
            Rectangle sourceRectangle,
            Rectangle targetRectangle,
            bool compand)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new ResizeProcessor<TPixel>(sampler, width, height, source.GetCurrentSize(), targetRectangle, compand), sourceRectangle);

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler and source rectangle.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="targetRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the target image object to draw to.
        /// </param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static IImageProcessingContext<TPixel> Resize<TPixel>(
            this IImageProcessingContext<TPixel> source,
            int width,
            int height,
            IResampler sampler,
            Rectangle targetRectangle,
            bool compand)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new ResizeProcessor<TPixel>(sampler, width, height, source.GetCurrentSize(), targetRectangle, compand));
    }
}