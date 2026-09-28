// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Normalization;

namespace SixLabors.ImageSharp.Processing;

/// <summary>
/// Defines extensions for automatic level adjustment.
/// </summary>
public static class AutoLevelExtensions
{
    /// <summary>
    /// Automatically adjusts the image's brightness levels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext AutoLevel(this IImageProcessingContext source)
    {
        HistogramEqualizationOptions options = new() { Method = HistogramEqualizationMethod.AutoLevel };
        return source.HistogramEqualization(options);
    }
}
