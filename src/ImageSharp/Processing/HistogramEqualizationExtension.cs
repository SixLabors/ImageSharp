// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Normalization;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extension that allow the adjustment of the contrast of an image via its histogram.
    /// </summary>
    public static class HistogramEqualizationExtension
    {
        /// <summary>
        /// Equalizes the histogram of an image to increases the global contrast using 65536 luminance levels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> HistogramEqualization<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => HistogramEqualization(source, 65536);

        /// <summary>
        /// Equalizes the histogram of an image to increases the global contrast.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> HistogramEqualization<TPixel>(this IImageProcessingContext<TPixel> source, int luminanceLevels)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new HistogramEqualizationProcessor<TPixel>(luminanceLevels));
    }
}
