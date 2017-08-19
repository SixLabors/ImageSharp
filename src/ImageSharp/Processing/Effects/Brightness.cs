// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
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
        /// Alters the brightness component of the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new brightness of the image. Must be between -100 and 100.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Brightness<TPixel>(this IImageProcessingContext<TPixel> source, int amount)
           where TPixel : struct, IPixel<TPixel>
        => source.ApplyProcessor(new BrightnessProcessor<TPixel>(amount));

        /// <summary>
        /// Alters the brightness component of the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new brightness of the image. Must be between -100 and 100.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> Brightness<TPixel>(this IImageProcessingContext<TPixel> source, int amount, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        => source.ApplyProcessor(new BrightnessProcessor<TPixel>(amount), rectangle);
    }
}
