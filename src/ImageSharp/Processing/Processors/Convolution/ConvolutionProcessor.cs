// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution;

/// <summary>
/// Defines a processor that uses a 2 dimensional matrix to perform convolution against an image.
/// </summary>
public class ConvolutionProcessor : IImageProcessor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConvolutionProcessor"/> class.
    /// </summary>
    /// <param name="kernelXY">The 2d gradient operator.</param>
    /// <param name="preserveAlpha">Whether the convolution filter is applied to alpha as well as the color channels.</param>
    /// <param name="borderWrapModeX">The <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in X direction.</param>
    /// <param name="borderWrapModeY">The <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in Y direction.</param>
    public ConvolutionProcessor(
        in DenseMatrix<float> kernelXY,
        bool preserveAlpha,
        BorderWrappingMode borderWrapModeX,
        BorderWrappingMode borderWrapModeY)
    {
        this.KernelXY = kernelXY;
        this.PreserveAlpha = preserveAlpha;
        this.BorderWrapModeX = borderWrapModeX;
        this.BorderWrapModeY = borderWrapModeY;
    }

    /// <summary>
    /// Gets the 2d convolution kernel.
    /// </summary>
    public DenseMatrix<float> KernelXY { get; }

    /// <summary>
    /// Gets a value indicating whether the convolution filter is applied to alpha as well as the color channels.
    /// </summary>
    public bool PreserveAlpha { get; }

    /// <summary>
    /// Gets the <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in X direction.
    /// </summary>
    public BorderWrappingMode BorderWrapModeX { get; }

    /// <summary>
    /// Gets the <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in Y direction.
    /// </summary>
    public BorderWrappingMode BorderWrapModeY { get; }

    /// <inheritdoc/>
    public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
        where TPixel : unmanaged,
        IPixel<TPixel>
    {
        if (this.KernelXY.TryGetLinearlySeparableComponents(out float[]? kernelX, out float[]? kernelY))
        {
            return new Convolution2PassProcessor<TPixel>(
                configuration,
                kernelX,
                kernelY,
                this.PreserveAlpha,
                source,
                sourceRectangle,
                this.BorderWrapModeX,
                this.BorderWrapModeY);
        }

        return new ConvolutionProcessor<TPixel>(
            configuration,
            this.KernelXY,
            this.PreserveAlpha,
            source,
            sourceRectangle,
            this.BorderWrapModeX,
            this.BorderWrapModeY);
    }
}
