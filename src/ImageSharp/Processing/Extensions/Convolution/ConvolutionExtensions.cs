// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Processing.Extensions.Convolution;

/// <summary>
/// Defines general convolution extensions to apply on an <see cref="Image"/>
/// using Mutate/Clone.
/// </summary>
public static class ConvolutionExtensions
{
    /// <summary>
    /// Applies a convolution filter to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernelXY">The convolution kernel to apply.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext Convolve(this IImageProcessingContext source, DenseMatrix<float> kernelXY)
        => Convolve(source, kernelXY, false);

    /// <summary>
    /// Applies a convolution filter to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernelXY">The convolution kernel to apply.</param>
    /// <param name="preserveAlpha">Whether the convolution filter is applied to alpha as well as the color channels.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext Convolve(this IImageProcessingContext source, DenseMatrix<float> kernelXY, bool preserveAlpha)
        => Convolve(source, kernelXY, preserveAlpha, BorderWrappingMode.Repeat, BorderWrappingMode.Repeat);

    /// <summary>
    /// Applies a convolution filter to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="kernelXY">The convolution kernel to apply.</param>
    /// <param name="preserveAlpha">Whether the convolution filter is applied to alpha as well as the color channels.</param>
    /// <param name="borderWrapModeX">The <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in X direction.</param>
    /// <param name="borderWrapModeY">The <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in Y direction.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext Convolve(
        this IImageProcessingContext source,
        DenseMatrix<float> kernelXY,
        bool preserveAlpha,
        BorderWrappingMode borderWrapModeX,
        BorderWrappingMode borderWrapModeY)
        => source.ApplyProcessor(new ConvolutionProcessor(kernelXY, preserveAlpha, borderWrapModeX, borderWrapModeY));

    /// <summary>
    /// Applies a convolution filter to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">The rectangle structure that specifies the portion of the image object to alter.</param>
    /// <param name="kernelXY">The convolution kernel to apply.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext Convolve(this IImageProcessingContext source, Rectangle rectangle, DenseMatrix<float> kernelXY)
        => Convolve(source, rectangle, kernelXY, false);

    /// <summary>
    /// Applies a convolution filter to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">The rectangle structure that specifies the portion of the image object to alter.</param>
    /// <param name="kernelXY">The convolution kernel to apply.</param>
    /// <param name="preserveAlpha">Whether the convolution filter is applied to alpha as well as the color channels.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext Convolve(this IImageProcessingContext source, Rectangle rectangle, DenseMatrix<float> kernelXY, bool preserveAlpha)
        => Convolve(source, rectangle, kernelXY, preserveAlpha, BorderWrappingMode.Repeat, BorderWrappingMode.Repeat);

    /// <summary>
    /// Applies a convolution filter to the image.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="rectangle">The rectangle structure that specifies the portion of the image object to alter.</param>
    /// <param name="kernelXY">The convolution kernel to apply.</param>
    /// <param name="preserveAlpha">Whether the convolution filter is applied to alpha as well as the color channels.</param>
    /// <param name="borderWrapModeX">The <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in X direction.</param>
    /// <param name="borderWrapModeY">The <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in Y direction.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext Convolve(
        this IImageProcessingContext source,
        Rectangle rectangle,
        DenseMatrix<float> kernelXY,
        bool preserveAlpha,
        BorderWrappingMode borderWrapModeX,
        BorderWrappingMode borderWrapModeY)
        => source.ApplyProcessor(new ConvolutionProcessor(kernelXY, preserveAlpha, borderWrapModeX, borderWrapModeY), rectangle);
}
