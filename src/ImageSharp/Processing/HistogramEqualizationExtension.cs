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
        /// Equalizes the histogram of an image to increases the contrast.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> HistogramEqualization<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => HistogramEqualization(source, HistogramEqualizationOptions.Default);

        /// <summary>
        /// Equalizes the histogram of an image to increases the contrast.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The histogram equalization options to use.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> HistogramEqualization<TPixel>(this IImageProcessingContext<TPixel> source, HistogramEqualizationOptions options)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(GetProcessor<TPixel>(options));

        private static HistogramEqualizationProcessor<TPixel> GetProcessor<TPixel>(HistogramEqualizationOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            HistogramEqualizationProcessor<TPixel> processor;

            switch (options.Method)
            {
                case HistogramEqualizationMethod.Global:
                    processor = new GlobalHistogramEqualizationProcessor<TPixel>(options.LuminanceLevels, options.ClipHistogram, options.ClipLimitPercentage);
                    break;

                case HistogramEqualizationMethod.AdaptiveTileInterpolation:
                    processor = new AdaptiveHistEqualizationProcessor<TPixel>(options.LuminanceLevels, options.ClipHistogram, options.ClipLimitPercentage, options.Tiles);
                    break;

                case HistogramEqualizationMethod.AdaptiveSlidingWindow:
                    processor = new AdaptiveHistEqualizationSWProcessor<TPixel>(options.LuminanceLevels, options.ClipHistogram, options.ClipLimitPercentage, options.Tiles);
                    break;

                default:
                    processor = new GlobalHistogramEqualizationProcessor<TPixel>(options.LuminanceLevels, options.ClipHistogram, options.ClipLimitPercentage);
                    break;
            }

            return processor;
        }
    }
}
