// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Processing;

/// <summary>
/// Defines Gaussian blurring extensions to apply on an <see cref="Image"/>
/// using Mutate/Clone.
/// </summary>
public static class GaussianBlurExtensions
{
    /// <summary>
    /// Applies a Gaussian blur to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext GaussianBlur(this IImageProcessingContext source)
        => source.ApplyProcessor(new GaussianBlurProcessor());

    /// <summary>
    /// Applies a Gaussian blur to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext GaussianBlur(this IImageProcessingContext source, float sigma)
        => source.ApplyProcessor(new GaussianBlurProcessor(sigma));

    /// <summary>
    /// Applies a Gaussian blur to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext GaussianBlur(
        this IImageProcessingContext source,
        Rectangle rectangle,
        float sigma)
        => source.ApplyProcessor(new GaussianBlurProcessor(sigma), rectangle);

    /// <summary>
    /// Applies a Gaussian blur to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
    /// <param name="borderWrapModeX">
    /// The <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in X direction.
    /// </param>
    /// <param name="borderWrapModeY">
    /// The <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in Y direction.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext GaussianBlur(
        this IImageProcessingContext source,
        Rectangle rectangle,
        float sigma,
        BorderWrappingMode borderWrapModeX,
        BorderWrappingMode borderWrapModeY)
        => source.ApplyProcessor(new GaussianBlurProcessor(sigma, borderWrapModeX, borderWrapModeY), rectangle);
}
