// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    internal class ResizeWorker<TPixel> : IDisposable
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly Buffer2D<Vector4> buffer;

        private readonly Configuration configuration;

        private readonly PixelConversionModifiers conversionModifiers;

        private readonly ResizeKernelMap horizontalKernelMap;

        private readonly BufferArea<TPixel> source;

        private readonly Rectangle sourceRectangle;

        private readonly int startX;

        private readonly IMemoryOwner<Vector4> tempRowBuffer;

        private readonly IMemoryOwner<Vector4> tempColumnBuffer;

        private readonly ResizeKernelMap verticalKernelMap;

        private readonly int destWidth;

        private readonly Rectangle destWorkingRect;

        private readonly int diameter;

        private readonly int windowHeight;

        public ResizeWorker(
            Configuration configuration,
            BufferArea<TPixel> source,
            PixelConversionModifiers conversionModifiers,
            ResizeKernelMap horizontalKernelMap,
            ResizeKernelMap verticalKernelMap,
            int destWidth,
            Rectangle destWorkingRect,
            int startX)
        {
            this.configuration = configuration;
            this.source = source;
            this.sourceRectangle = source.Rectangle;
            this.conversionModifiers = conversionModifiers;
            this.horizontalKernelMap = horizontalKernelMap;
            this.verticalKernelMap = verticalKernelMap;
            this.destWidth = destWidth;
            this.destWorkingRect = destWorkingRect;
            this.startX = startX;

            this.diameter = verticalKernelMap.MaxDiameter;

            this.windowHeight = Math.Min(this.sourceRectangle.Height, 2 * this.diameter);

            this.buffer = configuration.MemoryAllocator.Allocate2D<Vector4>(
                this.windowHeight,
                destWidth,
                AllocationOptions.Clean);

            this.tempRowBuffer = configuration.MemoryAllocator.Allocate<Vector4>(this.sourceRectangle.Width);
            this.tempColumnBuffer = configuration.MemoryAllocator.Allocate<Vector4>(destWidth);

            this.CurrentMinY = 0;
            this.CurrentMaxY = this.windowHeight;
        }

        public int CurrentMaxY { get; private set; }

        public int CurrentMinY { get; private set; }

        public void Dispose()
        {
            this.buffer.Dispose();
            this.tempRowBuffer.Dispose();
            this.tempColumnBuffer.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<Vector4> GetColumnSpan(int x, int startY)
        {
            return this.buffer.GetRowSpan(x).Slice(startY - this.CurrentMinY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<Vector4> GetColumnSpan(int x)
        {
            return this.buffer.GetRowSpan(x);
        }

        public void Initialize()
        {
            this.CalculateFirstPassValues(0, this.windowHeight);
        }

        public void FillDestinationPixels(int minY, int maxY, int startY, Buffer2D<TPixel> destination)
        {
            Span<Vector4> tempColSpan = this.tempColumnBuffer.GetSpan();

            for (int y = minY; y < maxY; y++)
            {
                // Ensure offsets are normalized for cropping and padding.
                ResizeKernel kernel = this.verticalKernelMap.GetKernel(y - startY);

                while (kernel.StartIndex + kernel.Length > this.CurrentMaxY)
                {
                    this.Slide();
                }

                ref Vector4 tempRowBase = ref MemoryMarshal.GetReference(tempColSpan);

                int top = kernel.StartIndex - this.CurrentMinY;

                for (int x = 0; x < this.destWidth; x++)
                {
                    Span<Vector4> firstPassColumn = this.GetColumnSpan(x).Slice(top);

                    // Destination color components
                    Unsafe.Add(ref tempRowBase, x) = kernel.ConvolveCore(firstPassColumn);
                }

                Span<TPixel> targetRowSpan = destination.GetRowSpan(y);

                PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, tempColSpan, targetRowSpan, conversionModifiers);
            }
        }

        public void Slide()
        {
            this.CurrentMinY = this.CurrentMinY + this.diameter;
            this.CurrentMaxY = Math.Min(this.CurrentMaxY + this.diameter, this.sourceRectangle.Height);
            this.CalculateFirstPassValues(this.CurrentMinY, this.CurrentMaxY);
        }

        private void CalculateFirstPassValues(int minY, int maxY)
        {
            Span<Vector4> tempRowSpan = this.tempRowBuffer.GetSpan();
            for (int y = minY; y < maxY; y++)
            {
                Span<TPixel> sourceRow = this.source.GetRowSpan(y);

                PixelOperations<TPixel>.Instance.ToVector4(
                    this.configuration,
                    sourceRow,
                    tempRowSpan,
                    this.conversionModifiers);

                //ref Vector4 firstPassBaseRef = ref this.buffer.Span[y - top];
                Span<Vector4> firstPassSpan = this.buffer.Span.Slice(y - minY);

                for (int x = this.destWorkingRect.Left; x < this.destWorkingRect.Right; x++)
                {
                    ResizeKernel kernel = this.horizontalKernelMap.GetKernel(x - this.startX);
                    firstPassSpan[x * this.windowHeight] = kernel.Convolve(tempRowSpan);
                    //Unsafe.Add(ref firstPassBaseRef, x * this.sourceRectangle.Height) = kernel.Convolve(tempRowSpan);
                }
            }
        }
    }
}