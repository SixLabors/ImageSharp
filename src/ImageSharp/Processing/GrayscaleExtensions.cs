// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the application of grayscale toning to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class GrayscaleExtensions
    {
        /// <summary>
        /// Applies <see cref="GrayscaleMode.Bt709"/> grayscale toning to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Grayscale<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => Grayscale(source, GrayscaleMode.Bt709);

        /// <summary>
        /// Applies <see cref="GrayscaleMode.Bt709"/> grayscale toning to the image using the given amount.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Grayscale<TPixel>(this IImageProcessingContext<TPixel> source, float amount)
            where TPixel : struct, IPixel<TPixel>
            => Grayscale(source, GrayscaleMode.Bt709, amount);

        /// <summary>
        /// Applies grayscale toning to the image with the given <see cref="GrayscaleMode"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Grayscale<TPixel>(this IImageProcessingContext<TPixel> source, GrayscaleMode mode)
            where TPixel : struct, IPixel<TPixel>
            => Grayscale(source, mode, 1F);

        /// <summary>
        /// Applies grayscale toning to the image with the given <see cref="GrayscaleMode"/> using the given amount.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Grayscale<TPixel>(this IImageProcessingContext<TPixel> source, GrayscaleMode mode, float amount)
            where TPixel : struct, IPixel<TPixel>
        {
            IImageProcessor<TPixel> processor = mode == GrayscaleMode.Bt709
               ? (IImageProcessor<TPixel>)new GrayscaleBt709Processor<TPixel>(amount)
               : new GrayscaleBt601Processor<TPixel>(1F);

            source.ApplyProcessor(processor);
            return source;
        }

        /// <summary>
        /// Applies <see cref="GrayscaleMode.Bt709"/> grayscale toning to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Grayscale<TPixel>(this IImageProcessingContext<TPixel> source, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => Grayscale(source, 1F, rectangle);

        /// <summary>
        /// Applies <see cref="GrayscaleMode.Bt709"/> grayscale toning to the image using the given amount.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Grayscale<TPixel>(this IImageProcessingContext<TPixel> source, float amount, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => Grayscale(source, GrayscaleMode.Bt709, amount, rectangle);

        /// <summary>
        /// Applies grayscale toning to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Grayscale<TPixel>(this IImageProcessingContext<TPixel> source, GrayscaleMode mode, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => Grayscale(source, mode, 1F, rectangle);

        /// <summary>
        /// Applies grayscale toning to the image using the given amount.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <param name="amount">The proportion of the conversion. Must be between 0 and 1.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Grayscale<TPixel>(this IImageProcessingContext<TPixel> source, GrayscaleMode mode, float amount, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            IImageProcessor<TPixel> processor = mode == GrayscaleMode.Bt709
                ? (IImageProcessor<TPixel>)new GrayscaleBt709Processor<TPixel>(amount)
                : new GrayscaleBt601Processor<TPixel>(amount);

            source.ApplyProcessor(processor, rectangle);
            return source;
        }
    }
}