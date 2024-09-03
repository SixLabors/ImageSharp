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
    public static IImageProcessingContext DetectEdges(this IImageProcessingContext source) =>
        DetectEdges(source, KnownEdgeDetectorKernels.Sobel);

    /// <summary>
    /// Detects any edges within the image.
    /// Uses the <see cref="KnownEdgeDetectorKernels.Sobel"/> kernel operating in grayscale mode.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        Rectangle rectangle) =>
        DetectEdges(source, KnownEdgeDetectorKernels.Sobel, rectangle);

    /// <summary>
    /// Detects any edges within the image operating in grayscale mode.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">The 2D edge detector kernel.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        EdgeDetector2DKernel kernel) =>
        DetectEdges(source, kernel, true);

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
    {
        EdgeDetector2DProcessor processor = new EdgeDetector2DProcessor(kernel, grayscale);
        source.ApplyProcessor(processor);
        return source;
    }

    /// <summary>
    /// Detects any edges within the image operating in grayscale mode.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">The 2D edge detector kernel.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        EdgeDetector2DKernel kernel,
        Rectangle rectangle) =>
        DetectEdges(source, kernel, true, rectangle);

    /// <summary>
    /// Detects any edges within the image using a <see cref="EdgeDetector2DKernel"/>.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">The 2D edge detector kernel.</param>
    /// <param name="grayscale">
    /// Whether to convert the image to grayscale before performing edge detection.
    /// </param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        EdgeDetector2DKernel kernel,
        bool grayscale,
        Rectangle rectangle)
    {
        EdgeDetector2DProcessor processor = new EdgeDetector2DProcessor(kernel, grayscale);
        source.ApplyProcessor(processor, rectangle);
        return source;
    }

    /// <summary>
    /// Detects any edges within the image operating in grayscale mode.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">The edge detector kernel.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        EdgeDetectorKernel kernel) =>
        DetectEdges(source, kernel, true);

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
    {
        EdgeDetectorProcessor processor = new EdgeDetectorProcessor(kernel, grayscale);
        source.ApplyProcessor(processor);
        return source;
    }

    /// <summary>
    /// Detects any edges within the image operating in grayscale mode.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">The edge detector kernel.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        EdgeDetectorKernel kernel,
        Rectangle rectangle) =>
        DetectEdges(source, kernel, true, rectangle);

    /// <summary>
    /// Detects any edges within the image using a <see cref="EdgeDetectorKernel"/>.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">The edge detector kernel.</param>
    /// <param name="grayscale">
    /// Whether to convert the image to grayscale before performing edge detection.
    /// </param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        EdgeDetectorKernel kernel,
        bool grayscale,
        Rectangle rectangle)
    {
        EdgeDetectorProcessor processor = new EdgeDetectorProcessor(kernel, grayscale);
        source.ApplyProcessor(processor, rectangle);
        return source;
    }

    /// <summary>
    /// Detects any edges within the image operating in grayscale mode.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">Thecompass edge detector kernel.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        EdgeDetectorCompassKernel kernel) =>
        DetectEdges(source, kernel, true);

    /// <summary>
    /// Detects any edges within the image using a <see cref="EdgeDetectorCompassKernel"/>.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">Thecompass edge detector kernel.</param>
    /// <param name="grayscale">
    /// Whether to convert the image to grayscale before performing edge detection.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        EdgeDetectorCompassKernel kernel,
        bool grayscale)
    {
        EdgeDetectorCompassProcessor processor = new EdgeDetectorCompassProcessor(kernel, grayscale);
        source.ApplyProcessor(processor);
        return source;
    }

    /// <summary>
    /// Detects any edges within the image operating in grayscale mode.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">Thecompass edge detector kernel.</param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        EdgeDetectorCompassKernel kernel,
        Rectangle rectangle) =>
        DetectEdges(source, kernel, true, rectangle);

    /// <summary>
    /// Detects any edges within the image using a <see cref="EdgeDetectorCompassKernel"/>.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernel">Thecompass edge detector kernel.</param>
    /// <param name="grayscale">
    /// Whether to convert the image to grayscale before performing edge detection.
    /// </param>
    /// <param name="rectangle">
    /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DetectEdges(
        this IImageProcessingContext source,
        EdgeDetectorCompassKernel kernel,
        bool grayscale,
        Rectangle rectangle)
    {
        EdgeDetectorCompassProcessor processor = new EdgeDetectorCompassProcessor(kernel, grayscale);
        source.ApplyProcessor(processor, rectangle);
        return source;
    }
}
