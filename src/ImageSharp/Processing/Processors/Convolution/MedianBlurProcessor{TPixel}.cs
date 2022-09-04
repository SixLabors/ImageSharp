// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies an median filter.
    /// </summary>
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

            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());

            // We use a rectangle with width set wider, to allocate a buffer big enough
            // for kernel source, channel buffers, source rows and target bulk pixel conversion.
            int operationWidth = (2 * kernelSize * kernelSize) + interest.Width + (kernelSize * interest.Width);
            var operationBounds = new Rectangle(interest.X, interest.Y, operationWidth, interest.Height);

            using var map = new KernelSamplingMap(this.Configuration.MemoryAllocator);
            map.BuildSamplingOffsetMap(kernelSize, kernelSize, interest, this.definition.BorderWrapModeX, this.definition.BorderWrapModeY);

            var operation = new MedianRowOperation<TPixel>(
                interest,
                targetPixels,
                source.PixelBuffer,
                map,
                kernelSize,
                this.Configuration,
                this.definition.PreserveAlpha);

            ParallelRowIterator.IterateRows<MedianRowOperation<TPixel>, Vector4>(
                this.Configuration,
                operationBounds,
                in operation);

            Buffer2D<TPixel>.SwapOrCopyContent(source.PixelBuffer, targetPixels);
        }
    }
}
