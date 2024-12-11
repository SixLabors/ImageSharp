// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution;

/// <summary>
/// Applies an median filter.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal sealed class MedianBlurProcessor<TPixel> : ImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly MedianBlurProcessor definition;

    public MedianBlurProcessor(Configuration configuration, MedianBlurProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle) => this.definition = definition;

    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        int kernelSize = (2 * this.definition.Radius) + 1;

        MemoryAllocator allocator = this.Configuration.MemoryAllocator;
        using Buffer2D<TPixel> targetPixels = allocator.Allocate2D<TPixel>(source.Width, source.Height);

        source.CopyTo(targetPixels);

        Rectangle interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds);

        using KernelSamplingMap map = new(this.Configuration.MemoryAllocator);
        map.BuildSamplingOffsetMap(kernelSize, kernelSize, interest, this.definition.BorderWrapModeX, this.definition.BorderWrapModeY);

        MedianRowOperation<TPixel> operation = new(
            interest,
            targetPixels,
            source.PixelBuffer,
            map,
            kernelSize,
            this.Configuration,
            this.definition.PreserveAlpha);

        ParallelRowIterator.IterateRows<MedianRowOperation<TPixel>, Vector4>(
            this.Configuration,
            interest,
            in operation);

        Buffer2D<TPixel>.SwapOrCopyContent(source.PixelBuffer, targetPixels);
    }
}
