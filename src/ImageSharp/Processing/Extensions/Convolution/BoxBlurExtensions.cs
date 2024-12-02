// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Processing;

/// <summary>
/// Defines extensions methods to apply box blurring to an <see cref="Image"/>
/// using Mutate/Clone.
/// </summary>
public static class BoxBlurExtensions
{
    /// <summary>
    /// Applies a box blur to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext BoxBlur(this IImageProcessingContext source)
        => source.ApplyProcessor(new BoxBlurProcessor());

    /// <summary>
    /// Applies a box blur to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext BoxBlur(this IImageProcessingContext source, int radius)
        => source.ApplyProcessor(new BoxBlurProcessor(radius));

    /// <summary>
    /// Applies a box blur to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext BoxBlur(this IImageProcessingContext source, int radius, Rectangle rectangle)
        => source.ApplyProcessor(new BoxBlurProcessor(radius), rectangle);

    /// <summary>
    /// Applies a box blur to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
    /// <param name="borderWrapModeX">
    /// The <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in X direction.
    /// </param>
    /// <param name="borderWrapModeY">
    /// The <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in Y direction.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext BoxBlur(
        this IImageProcessingContext source,
        Rectangle rectangle,
        int radius,
        BorderWrappingMode borderWrapModeX,
        BorderWrappingMode borderWrapModeY)
        => source.ApplyProcessor(new BoxBlurProcessor(radius, borderWrapModeX, borderWrapModeY), rectangle);
}
