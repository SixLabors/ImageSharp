// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Processing;

/// <summary>
/// Defines extensions that allow the applying of the median blur on an <see cref="Image"/>
/// using Mutate/Clone.
/// </summary>
public static class MedianBlurExtensions
{
    /// <summary>
    /// Applies a median blur on the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="radius">The radius of the area to find the median for.</param>
    /// <param name="preserveAlpha">
    /// Whether the filter is applied to alpha as well as the color channels.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext MedianBlur(
        this IImageProcessingContext source,
        int radius,
        bool preserveAlpha)
        => source.ApplyProcessor(new MedianBlurProcessor(radius, preserveAlpha));

    /// <summary>
    /// Applies a median blur on the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <param name="radius">The radius of the area to find the median for.</param>
    /// <param name="preserveAlpha">
    /// Whether the filter is applied to alpha as well as the color channels.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext MedianBlur(
        this IImageProcessingContext source,
        Rectangle rectangle,
        int radius,
        bool preserveAlpha)
        => source.ApplyProcessor(new MedianBlurProcessor(radius, preserveAlpha), rectangle);
}
