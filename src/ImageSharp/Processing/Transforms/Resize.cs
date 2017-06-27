// <copyright file="Resize.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using ImageSharp.PixelFormats;

    using ImageSharp.Processing;
    using Processing.Processors;
    using SixLabors.Primitives;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Resizes an image in accordance with the given <see cref="ResizeOptions"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="options">The resize options.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        /// <remarks>Passing zero for one of height or width within the resize options will automatically preserve the aspect ratio of the original image</remarks>
        public static Image<TPixel> Resize<TPixel>(this Image<TPixel> source, ResizeOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            // Ensure size is populated across both dimensions.
            if (options.Size.Width == 0 && options.Size.Height > 0)
            {
                options.Size = new Size((int)MathF.Round(source.Width * options.Size.Height / (float)source.Height), options.Size.Height);
            }

            if (options.Size.Height == 0 && options.Size.Width > 0)
            {
                options.Size = new Size(options.Size.Width, (int)MathF.Round(source.Height * options.Size.Width / (float)source.Width));
            }

            Rectangle targetRectangle = ResizeHelper.CalculateTargetLocationAndBounds(source, options);

            return Resize(source, options.Size.Width, options.Size.Height, options.Sampler, source.Bounds, targetRectangle, options.Compand);
        }

        /// <summary>
        /// Resizes an image to the given <see cref="Size"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="size">The target image size.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static Image<TPixel> Resize<TPixel>(this Image<TPixel> source, Size size)
            where TPixel : struct, IPixel<TPixel>
        {
            return Resize(source, size.Width, size.Height, new BicubicResampler(), false);
        }

        /// <summary>
        /// Resizes an image to the given <see cref="Size"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="size">The target image size.</param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static Image<TPixel> Resize<TPixel>(this Image<TPixel> source, Size size, bool compand)
            where TPixel : struct, IPixel<TPixel>
        {
            return Resize(source, size.Width, size.Height, new BicubicResampler(), compand);
        }

        /// <summary>
        /// Resizes an image to the given width and height.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static Image<TPixel> Resize<TPixel>(this Image<TPixel> source, int width, int height)
            where TPixel : struct, IPixel<TPixel>
        {
            return Resize(source, width, height, new BicubicResampler(), false);
        }

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
        public static Image<TPixel> Resize<TPixel>(this Image<TPixel> source, int width, int height, bool compand)
            where TPixel : struct, IPixel<TPixel>
        {
            return Resize(source, width, height, new BicubicResampler(), compand);
        }

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
        public static Image<TPixel> Resize<TPixel>(this Image<TPixel> source, int width, int height, IResampler sampler)
            where TPixel : struct, IPixel<TPixel>
        {
            return Resize(source, width, height, sampler, false);
        }

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
        public static Image<TPixel> Resize<TPixel>(this Image<TPixel> source, int width, int height, IResampler sampler, bool compand)
            where TPixel : struct, IPixel<TPixel>
        {
            return Resize(source, width, height, sampler, source.Bounds, new Rectangle(0, 0, width, height), compand);
        }

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
        public static Image<TPixel> Resize<TPixel>(this Image<TPixel> source, int width, int height, IResampler sampler, Rectangle sourceRectangle, Rectangle targetRectangle, bool compand)
            where TPixel : struct, IPixel<TPixel>
        {
            if (width == 0 && height > 0)
            {
                width = (int)MathF.Round(source.Width * height / (float)source.Height);
                targetRectangle.Width = width;
            }

            if (height == 0 && width > 0)
            {
                height = (int)MathF.Round(source.Height * width / (float)source.Width);
                targetRectangle.Height = height;
            }

            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            var processor = new ResizeProcessor<TPixel>(sampler, width, height, targetRectangle) { Compand = compand };

            source.ApplyProcessor(processor, sourceRectangle);
            return source;
        }
    }
}
