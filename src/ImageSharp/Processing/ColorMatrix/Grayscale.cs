// <copyright file="Grayscale.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using ImageSharp.PixelFormats;

    using ImageSharp.Processing;
    using Processing.Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies Grayscale toning to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Grayscale<TPixel>(this Image<TPixel> source, GrayscaleMode mode = GrayscaleMode.Bt709)
            where TPixel : struct, IPixel<TPixel>
        {
            return Grayscale(source, source.Bounds, mode);
        }

        /// <summary>
        /// Applies Grayscale toning to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Grayscale<TPixel>(this Image<TPixel> source, Rectangle rectangle, GrayscaleMode mode = GrayscaleMode.Bt709)
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
