// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Normalization;

namespace SixLabors.ImageSharp.Processing;

/// <summary>
/// Defines extension that allow the adjustment of the contrast of an image via its histogram.
/// </summary>
public static class HistogramEqualizationExtensions
{
    /// <summary>
    /// Equalizes the histogram of an image to increases the contrast.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext HistogramEqualization(this IImageProcessingContext source) =>
        HistogramEqualization(source, new HistogramEqualizationOptions());

    /// <summary>
    /// Equalizes the histogram of an image to increases the contrast.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="options">The histogram equalization options to use.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext HistogramEqualization(
        this IImageProcessingContext source,
        HistogramEqualizationOptions options) =>
        source.ApplyProcessor(HistogramEqualizationProcessor.FromOptions(options));
}
