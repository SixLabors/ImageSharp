// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Processing;

/// <summary>
/// Defines edge detection extensions applicable on an <see cref="Image"/> using Mutate/Clone.
/// </summary>
public static class DetectEdgesExtensions
{
    /// <summary>
    /// Detects any edges within the image.
    /// Uses the <see cref="KnownEdgeDetectorKernels.Sobel"/> kernel operating in grayscale mode.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(this IImageProcessingContext source)
        => DetectEdges(source, KnownEdgeDetectorKernels.Sobel);

    /// <summary>
    /// Detects any edges within the image.
    /// Uses the <see cref="KnownEdgeDetectorKernels.Sobel"/> kernel operating in grayscale mode.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(this IImageProcessingContext source, Rectangle rectangle)
        => DetectEdges(source, rectangle, KnownEdgeDetectorKernels.Sobel);

    /// <summary>
    /// Detects any edges within the image operating in grayscale mode.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">The 2D edge detector kernel.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(this IImageProcessingContext source, EdgeDetector2DKernel kernel)
        => DetectEdges(source, kernel, true);

    /// <summary>
    /// Detects any edges within the image using a <see cref="EdgeDetector2DKernel"/>.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">The 2D edge detector kernel.</param>
    /// <param name="grayscale">
    /// Whether to convert the image to grayscale before performing edge detection.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        EdgeDetector2DKernel kernel,
        bool grayscale)
        => source.ApplyProcessor(new EdgeDetector2DProcessor(kernel, grayscale));

    /// <summary>
    /// Detects any edges within the image operating in grayscale mode.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <param name="kernel">The 2D edge detector kernel.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        Rectangle rectangle,
        EdgeDetector2DKernel kernel)
        => DetectEdges(source, rectangle, kernel, true);

    /// <summary>
    /// Detects any edges within the image using a <see cref="EdgeDetector2DKernel"/>.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <param name="kernel">The 2D edge detector kernel.</param>
    /// <param name="grayscale">
    /// Whether to convert the image to grayscale before performing edge detection.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        Rectangle rectangle,
        EdgeDetector2DKernel kernel,
        bool grayscale)
        => source.ApplyProcessor(new EdgeDetector2DProcessor(kernel, grayscale), rectangle);

    /// <summary>
    /// Detects any edges within the image operating in grayscale mode.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">The edge detector kernel.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(this IImageProcessingContext source, EdgeDetectorKernel kernel)
        => DetectEdges(source, kernel, true);

    /// <summary>
    /// Detects any edges within the image using a <see cref="EdgeDetectorKernel"/>.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">The edge detector kernel.</param>
    /// <param name="grayscale">
    /// Whether to convert the image to grayscale before performing edge detection.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        EdgeDetectorKernel kernel,
        bool grayscale)
        => source.ApplyProcessor(new EdgeDetectorProcessor(kernel, grayscale));

    /// <summary>
    /// Detects any edges within the image operating in grayscale mode.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <param name="kernel">The edge detector kernel.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        Rectangle rectangle,
        EdgeDetectorKernel kernel)
        => DetectEdges(source, rectangle, kernel, true);

    /// <summary>
    /// Detects any edges within the image using a <see cref="EdgeDetectorKernel"/>.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <param name="kernel">The edge detector kernel.</param>
    /// <param name="grayscale">
    /// Whether to convert the image to grayscale before performing edge detection.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        Rectangle rectangle,
        EdgeDetectorKernel kernel,
        bool grayscale)
        => source.ApplyProcessor(new EdgeDetectorProcessor(kernel, grayscale), rectangle);

    /// <summary>
    /// Detects any edges within the image operating in grayscale mode.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">The compass edge detector kernel.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(this IImageProcessingContext source, EdgeDetectorCompassKernel kernel)
        => DetectEdges(source, kernel, true);

    /// <summary>
    /// Detects any edges within the image using a <see cref="EdgeDetectorCompassKernel"/>.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">The compass edge detector kernel.</param>
    /// <param name="grayscale">
    /// Whether to convert the image to grayscale before performing edge detection.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        EdgeDetectorCompassKernel kernel,
        bool grayscale)
        => source.ApplyProcessor(new EdgeDetectorCompassProcessor(kernel, grayscale));

    /// <summary>
    /// Detects any edges within the image operating in grayscale mode.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <param name="kernel">The compass edge detector kernel.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        Rectangle rectangle,
        EdgeDetectorCompassKernel kernel)
        => DetectEdges(source, rectangle, kernel, true);

    /// <summary>
    /// Detects any edges within the image using a <see cref="EdgeDetectorCompassKernel"/>.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <param name="kernel">The compass edge detector kernel.</param>
    /// <param name="grayscale">
    /// Whether to convert the image to grayscale before performing edge detection.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        Rectangle rectangle,
        EdgeDetectorCompassKernel kernel,
        bool grayscale)
        => source.ApplyProcessor(new EdgeDetectorCompassProcessor(kernel, grayscale), rectangle);
}
