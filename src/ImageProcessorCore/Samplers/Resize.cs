// <copyright file="Resize.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor, TPacked}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Resizes an image in accordance with the given <see cref="ResizeOptions"/>.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="options">The resize options.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/></returns>
        /// <remarks>Passing zero for one of height or width within the resize options will automatically preserve the aspect ratio of the original image</remarks>
        public static Image<TColor, TPacked> Resize<TColor, TPacked>(this Image<TColor, TPacked> source, ResizeOptions options, ProgressEventHandler progressHandler = null)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            // Ensure size is populated across both dimensions.
            if (options.Size.Width == 0 && options.Size.Height > 0)
            {
                options.Size = new Size(source.Width * options.Size.Height / source.Height, options.Size.Height);
            }

            if (options.Size.Height == 0 && options.Size.Width > 0)
            {
                options.Size = new Size(options.Size.Width, source.Height * options.Size.Width / source.Width);
            }

            Rectangle targetRectangle = ResizeHelper.CalculateTargetLocationAndBounds(source, options);

            return Resize(source, options.Size.Width, options.Size.Height, options.Sampler, source.Bounds, targetRectangle, options.Compand, progressHandler);
        }

        /// <summary>
        /// Resizes an image to the given width and height.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static Image<TColor, TPacked> Resize<TColor, TPacked>(this Image<TColor, TPacked> source, int width, int height, ProgressEventHandler progressHandler = null)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return Resize(source, width, height, new BicubicResampler(), false, progressHandler);
        }

        /// <summary>
        /// Resizes an image to the given width and height.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static Image<TColor, TPacked> Resize<TColor, TPacked>(this Image<TColor, TPacked> source, int width, int height, bool compand, ProgressEventHandler progressHandler = null)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return Resize(source, width, height, new BicubicResampler(), compand, progressHandler);
        }

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static Image<TColor, TPacked> Resize<TColor, TPacked>(this Image<TColor, TPacked> source, int width, int height, IResampler sampler, ProgressEventHandler progressHandler = null)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return Resize(source, width, height, sampler, false, progressHandler);
        }

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static Image<TColor, TPacked> Resize<TColor, TPacked>(this Image<TColor, TPacked> source, int width, int height, IResampler sampler, bool compand, ProgressEventHandler progressHandler = null)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return Resize(source, width, height, sampler, source.Bounds, new Rectangle(0, 0, width, height), compand, progressHandler);
        }

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler and
        /// source rectangle.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
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
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static Image<TColor, TPacked> Resize<TColor, TPacked>(this Image<TColor, TPacked> source, int width, int height, IResampler sampler, Rectangle sourceRectangle, Rectangle targetRectangle, bool compand = false, ProgressEventHandler progressHandler = null)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            if (width == 0 && height > 0)
            {
                width = source.Width * height / source.Height;
                targetRectangle.Width = width;
            }

            if (height == 0 && width > 0)
            {
                height = source.Height * width / source.Width;
                targetRectangle.Height = height;
            }

            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            ResamplingWeightedProcessor<TColor, TPacked> processor;

            if (compand)
            {
                processor = new CompandingResizeProcessor<TColor, TPacked>(sampler);
            }
            else
            {
                processor = new ResizeProcessor<TColor, TPacked>(sampler);
            }

            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(width, height, sourceRectangle, targetRectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }
    }
}
