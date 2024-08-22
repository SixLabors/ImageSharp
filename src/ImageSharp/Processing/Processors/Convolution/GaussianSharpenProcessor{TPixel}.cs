// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution;

/// <summary>
/// Applies Gaussian sharpening processing to the image.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal class GaussianSharpenProcessor<TPixel> : ImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GaussianSharpenProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="definition">The <see cref="GaussianSharpenProcessor"/> defining the processor parameters.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    /// <param name="borderWrapModeX">The <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in X direction.</param>
    /// <param name="borderWrapModeY">The <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in Y direction.</param>
    public GaussianSharpenProcessor(
        Configuration configuration,
        GaussianSharpenProcessor definition,
        Image<TPixel> source,
        Rectangle sourceRectangle,
        BorderWrappingMode borderWrapModeX,
        BorderWrappingMode borderWrapModeY)
        : base(configuration, source, sourceRectangle)
    {
        int kernelSize = (definition.Radius * 2) + 1;
        this.Kernel = ConvolutionProcessorHelpers.CreateGaussianSharpenKernel(kernelSize, definition.Sigma);
        this.BorderWrapModeX = borderWrapModeX;
        this.BorderWrapModeY = borderWrapModeY;
    }

    /// <summary>
    /// Gets the 1D convolution kernel.
    /// </summary>
    public float[] Kernel { get; }

    /// <summary>
    /// Gets the <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in X direction.
    /// </summary>
    public BorderWrappingMode BorderWrapModeX { get; }

    /// <summary>
    /// Gets the <see cref="BorderWrappingMode"/> to use when mapping the pixels outside of the border, in Y direction.
    /// </summary>
    public BorderWrappingMode BorderWrapModeY { get; }

    /// <inheritdoc/>
    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        using Convolution2PassProcessor<TPixel> processor = new(this.Configuration, this.Kernel, false, this.Source, this.SourceRectangle, this.BorderWrapModeX, this.BorderWrapModeY);

        processor.Apply(source);
    }
}
