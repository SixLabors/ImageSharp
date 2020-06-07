// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Normalization;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extension that allow the adjustment of the contrast of an image via its histogram.
    /// </summary>
    public static class HistogramEqualizationExtensions
    {
        /// <summary>
        /// Equalizes the histogram of an image to increases the contrast.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext HistogramEqualization(this IImageProcessingContext source) =>
            HistogramEqualization(source, HistogramEqualizationOptions.Default);

        /// <summary>
        /// Equalizes the histogram of an image to increases the contrast.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The histogram equalization options to use.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext HistogramEqualization(
            this IImageProcessingContext source,
            HistogramEqualizationOptions options) =>
            source.ApplyProcessor(HistogramEqualizationProcessor.FromOptions(options));
    }
}