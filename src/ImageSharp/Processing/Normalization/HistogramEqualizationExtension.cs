// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Normalization
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
        /// <param name="luminanceLevels">The number of different luminance levels. Typical values are 256 for 8-bit grayscale images
        /// or 65536 for 16-bit grayscale images.Defaults to 65536.</param>
        /// <returns>A histogram equalized grayscale image.</returns>
        public static IImageProcessingContext<TPixel> HistogramEqualization<TPixel>(this IImageProcessingContext<TPixel> source, int luminanceLevels = 65536)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new HistogramEqualizationProcessor<TPixel>(luminanceLevels));
    }
}
