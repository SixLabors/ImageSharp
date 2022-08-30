// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
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
            : base(configuration, source, sourceRectangle)
        {
            this.definition = definition;
        }

        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            int kernelSize = (2 * this.definition.Radius) + 1;

            MemoryAllocator allocator = this.Configuration.MemoryAllocator;
            using Buffer2D<TPixel> targetPixels = allocator.Allocate2D<TPixel>(source.Width, source.Height);

            source.CopyTo(targetPixels);

            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());

            // We use a rectangle with width set to 2 * kernelSize^2 + width, to allocate a buffer big enough
            // for kernel source and target bulk pixel conversion.
            var operationBounds = new Rectangle(interest.X, interest.Y, (2 * (kernelSize * kernelSize)) + interest.Width, interest.Height);

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

        private void ProcessSingleRow(PixelAccessor<TPixel> access, KernelSamplingMap map, int y, Span<TPixel> dest)
        {
            int kernelSize = (2 * this.definition.Radius) + 1;
            int kernelCount = kernelSize * kernelSize;
            using var vectorsBuffer = this.Configuration.MemoryAllocator.Allocate<Vector4>(kernelCount);
            var vectorsSpan = vectorsBuffer.Memory.Span;
            using var rowBuffer = this.Configuration.MemoryAllocator.Allocate<Vector4>(dest.Length);
            var rowSpan = rowBuffer.Memory.Span;
            using var componentsBuffer = this.Configuration.MemoryAllocator.Allocate<float>(kernelCount << 2);
            var componentsSpan = componentsBuffer.Memory.Span;
            var xs = componentsSpan.Slice(0, kernelCount);
            var ys = componentsSpan.Slice(kernelCount, kernelCount);
            var zs = componentsSpan.Slice(kernelCount << 1, kernelCount);
            var ws = componentsSpan.Slice(kernelCount * 3, kernelCount);
            var xOffsets = map.GetColumnOffsetSpan();
            var yOffsets = map.GetRowOffsetSpan();
            var baseYOffsetIndex = y * kernelSize;
            for (var x = 0; x < access.Width; x++)
            {
                var baseXOffsetIndex = x * kernelSize;
                var index = 0;
                for (var w = 0; w < kernelSize; w++)
                {
                    var j = yOffsets[baseYOffsetIndex + w];
                    var row = access.GetRowSpan(j);
                    for (var z = 0; z < kernelSize; z++)
                    {
                        var k = xOffsets[baseXOffsetIndex + z];
                        var pixel = row[k];
                        vectorsSpan[index + z] = pixel.ToVector4();
                    }

                    index += kernelSize;
                }

                rowSpan[x] = this.FindMedian4(vectorsSpan, xs, ys, zs, ws, kernelCount);
            }

            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.Configuration, rowSpan, dest);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector4 FindMedian3(Span<Vector4> span, Span<float> xs, Span<float> ys, Span<float> zs, Span<float> ws, int stride)
        {
            Vector4 found = new Vector4(0f, 0f, 0f, 0f);
            int halfLength = (span.Length + 1) >> 1;

            // Find median of X component.
            for (int i = 0; i < xs.Length; i++)
            {
                xs[i] = span[i].X;
                ys[i] = span[i].Y;
                zs[i] = span[i].Z;
            }

            xs.Sort();
            ys.Sort();
            zs.Sort();

            return new Vector4(xs[halfLength], ys[halfLength], zs[halfLength], span[halfLength].W);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector4 FindMedian4(Span<Vector4> span, Span<float> xs, Span<float> ys, Span<float> zs, Span<float> ws, int stride)
        {
            Vector4 found = new Vector4(0f, 0f, 0f, 0f);
            int halfLength = (span.Length + 1) >> 1;

            // Find median of X component.
            for (int i = 0; i < xs.Length; i++)
            {
                xs[i] = span[i].X;
                ys[i] = span[i].Y;
                zs[i] = span[i].Z;
                ws[i] = span[i].W;
            }

            xs.Sort();
            ys.Sort();
            zs.Sort();
            ws.Sort();

            return new Vector4(xs[halfLength], ys[halfLength], zs[halfLength], ws[halfLength]);
        }
    }
}
