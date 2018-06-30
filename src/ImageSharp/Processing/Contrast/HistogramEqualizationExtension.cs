// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Contrast
{
    /// <summary>
    /// Adds extension that allows applying an HistogramEqualization to the image.
    /// </summary>
    public static class HistogramEqualizationExtension
    {
        /// <summary>
        /// Equalizes the histogram of an image to increases the global contrast.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> HistogramEqualization<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new HistogramEqualizationProcessor<TPixel>());
    }
}
