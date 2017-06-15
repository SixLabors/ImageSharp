// <copyright file="Grayscale.cs" company="James Jackson-South">
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
        /// Applies <see cref="GrayscaleMode.Bt709"/> Grayscale toning to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Grayscale<TPixel>(this Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
        {
            return Grayscale(source, GrayscaleMode.Bt709);
        }

        /// <summary>
        /// Applies Grayscale toning to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Grayscale<TPixel>(this Image<TPixel> source, GrayscaleMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            return Grayscale(source, mode, source.Bounds);
        }

        /// <summary>
        /// Applies <see cref="GrayscaleMode.Bt709"/> Grayscale toning to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Grayscale<TPixel>(this Image<TPixel> source, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            IImageProcessor<TPixel> processor = new GrayscaleBt709Processor<TPixel>();

            source.ApplyProcessor(processor, rectangle);
            return source;
        }

        /// <summary>
        /// Applies Grayscale toning to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Grayscale<TPixel>(this Image<TPixel> source, GrayscaleMode mode, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            IImageProcessor<TPixel> processor = mode == GrayscaleMode.Bt709
                ? (IImageProcessor<TPixel>)new GrayscaleBt709Processor<TPixel>()
                : new GrayscaleBt601Processor<TPixel>();

            source.ApplyProcessor(processor, rectangle);
            return source;
        }
    }
}
