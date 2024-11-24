// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Processing;

/// <summary>
/// Adds bokeh blurring extensions to the <see cref="Image{TPixel}"/> type.
/// </summary>
public static class BokehBlurExtensions
{
    /// <summary>
    /// Applies a bokeh blur to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext BokehBlur(this IImageProcessingContext source)
        => source.ApplyProcessor(new BokehBlurProcessor());

    /// <summary>
    /// Applies a bokeh blur to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
    /// <param name="components">The 'components' value representing the number of kernels to use to approximate the bokeh effect.</param>
    /// <param name="gamma">The gamma highlight factor to use to emphasize bright spots in the source image</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext BokehBlur(this IImageProcessingContext source, int radius, int components, float gamma)
        => source.ApplyProcessor(new BokehBlurProcessor(radius, components, gamma));

    /// <summary>
    /// Applies a bokeh blur to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext BokehBlur(this IImageProcessingContext source, Rectangle rectangle)
        => source.ApplyProcessor(new BokehBlurProcessor(), rectangle);

    /// <summary>
    /// Applies a bokeh blur to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
    /// <param name="components">The 'components' value representing the number of kernels to use to approximate the bokeh effect.</param>
    /// <param name="gamma">The gamma highlight factor to use to emphasize bright spots in the source image</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext BokehBlur(this IImageProcessingContext source, Rectangle rectangle, int radius, int components, float gamma)
        => source.ApplyProcessor(new BokehBlurProcessor(radius, components, gamma), rectangle);
}
