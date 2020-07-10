// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the application of resize operations on an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class ResizeExtensions
    {
        /// <summary>
        /// Resizes an image to the given <see cref="Size"/>.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="size">The target image size.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image or the nearest possible ratio.</remarks>
        public static IImageProcessingContext Resize(this IImageProcessingContext source, Size size)
            => Resize(source, size.Width, size.Height, KnownResamplers.Bicubic, false);

        /// <summary>
        /// Resizes an image to the given <see cref="Size"/>.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="size">The target image size.</param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image or the nearest possible ratio.</remarks>
        public static IImageProcessingContext Resize(this IImageProcessingContext source, Size size, bool compand)
            => Resize(source, size.Width, size.Height, KnownResamplers.Bicubic, compand);

        /// <summary>
        /// Resizes an image to the given width and height.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image or the nearest possible ratio.</remarks>
        public static IImageProcessingContext Resize(this IImageProcessingContext source, int width, int height)
            => Resize(source, width, height, KnownResamplers.Bicubic, false);

        /// <summary>
        /// Resizes an image to the given width and height.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image or the nearest possible ratio.</remarks>
        public static IImageProcessingContext Resize(this IImageProcessingContext source, int width, int height, bool compand)
            => Resize(source, width, height, KnownResamplers.Bicubic, compand);

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image or the nearest possible ratio.</remarks>
        public static IImageProcessingContext Resize(this IImageProcessingContext source, int width, int height, IResampler sampler)
            => Resize(source, width, height, sampler, false);

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="size">The target image size.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image or the nearest possible ratio.</remarks>
        public static IImageProcessingContext Resize(this IImageProcessingContext source, Size size, IResampler sampler, bool compand)
            => Resize(source, size.Width, size.Height, sampler, new Rectangle(0, 0, size.Width, size.Height), compand);

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image or the nearest possible ratio.</remarks>
        public static IImageProcessingContext Resize(this IImageProcessingContext source, int width, int height, IResampler sampler, bool compand)
            => Resize(source, width, height, sampler, new Rectangle(0, 0, width, height), compand);

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler and
        /// source rectangle.
        /// </summary>
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
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image or the nearest possible ratio.</remarks>
        public static IImageProcessingContext Resize(
            this IImageProcessingContext source,
            int width,
            int height,
            IResampler sampler,
            Rectangle sourceRectangle,
            Rectangle targetRectangle,
            bool compand)
        {
            var options = new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Manual,
                Sampler = sampler,
                TargetRectangle = targetRectangle,
                Compand = compand
            };

            return source.ApplyProcessor(new ResizeProcessor(options, source.GetCurrentSize()), sourceRectangle);
        }

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler and source rectangle.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="targetRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the target image object to draw to.
        /// </param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image or the nearest possible ratio.</remarks>
        public static IImageProcessingContext Resize(
            this IImageProcessingContext source,
            int width,
            int height,
            IResampler sampler,
            Rectangle targetRectangle,
            bool compand)
        {
            var options = new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Manual,
                Sampler = sampler,
                TargetRectangle = targetRectangle,
                Compand = compand
            };

            return Resize(source, options);
        }

        /// <summary>
        /// Resizes an image in accordance with the given <see cref="ResizeOptions"/>.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="options">The resize options.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        /// <remarks>Passing zero for one of height or width within the resize options will automatically preserve the aspect ratio of the original image or the nearest possible ratio.</remarks>
        public static IImageProcessingContext Resize(this IImageProcessingContext source, ResizeOptions options)
            => source.ApplyProcessor(new ResizeProcessor(options, source.GetCurrentSize()));
    }
}
