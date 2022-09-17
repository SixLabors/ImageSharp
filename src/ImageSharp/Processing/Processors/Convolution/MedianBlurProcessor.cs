// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution;

/// <summary>
/// Applies an median filter.
/// </summary>
public sealed class MedianBlurProcessor : IImageProcessor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MedianBlurProcessor"/> class.
    /// </summary>
    /// <param name="radius">
    /// The 'radius' value representing the size of the area to filter over.
    /// </param>
    /// <param name="preserveAlpha">
    /// Whether the filter is applied to alpha as well as the color channels.
    /// </param>
    public MedianBlurProcessor(int radius, bool preserveAlpha)
    {
        this.Radius = radius;
        this.PreserveAlpha = preserveAlpha;
    }

    /// <summary>
    /// Gets the size of the area to find the median of.
    /// </summary>
    public int Radius { get; }

    /// <summary>
    /// Gets a value indicating whether the filter is applied to alpha as well as the color channels.
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

    /// <inheritdoc />
    public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
        where TPixel : unmanaged, IPixel<TPixel>
        => new MedianBlurProcessor<TPixel>(configuration, this, source, sourceRectangle);
}
