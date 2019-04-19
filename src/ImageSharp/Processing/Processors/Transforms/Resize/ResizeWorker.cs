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
        private readonly Buffer2D<Vector4> transposedFirstPassBuffer;

        private readonly Configuration configuration;

        private readonly PixelConversionModifiers conversionModifiers;

        private readonly ResizeKernelMap horizontalKernelMap;

        private readonly BufferArea<TPixel> source;

        private readonly Rectangle sourceRectangle;

        private readonly IMemoryOwner<Vector4> tempRowBuffer;

        private readonly IMemoryOwner<Vector4> tempColumnBuffer;

        private readonly ResizeKernelMap verticalKernelMap;

        private readonly int destWidth;

        private readonly Rectangle targetWorkingRect;

        private readonly Point targetOrigin;

        private readonly int windowBandDiameter;

        private readonly int windowHeight;

        private RowInterval currentWindow;

        public ResizeWorker(
            Configuration configuration,
            BufferArea<TPixel> source,
            PixelConversionModifiers conversionModifiers,
            ResizeKernelMap horizontalKernelMap,
            ResizeKernelMap verticalKernelMap,
            int destWidth,
            Rectangle targetWorkingRect,
            Point targetOrigin)
        {
            this.configuration = configuration;
            this.source = source;
            this.sourceRectangle = source.Rectangle;
            this.conversionModifiers = conversionModifiers;
            this.horizontalKernelMap = horizontalKernelMap;
            this.verticalKernelMap = verticalKernelMap;
            this.destWidth = destWidth;
            this.targetWorkingRect = targetWorkingRect;
            this.targetOrigin = targetOrigin;

            this.windowBandDiameter = verticalKernelMap.MaxDiameter;

            int numberOfWindowBands = ResizeHelper.CalculateResizeWorkerHeightInWindowBands(
                this.windowBandDiameter,
                destWidth,
                configuration.WorkingBufferSizeHintInBytes);

            this.windowHeight = Math.Min(this.sourceRectangle.Height, numberOfWindowBands * this.windowBandDiameter);

            this.transposedFirstPassBuffer = configuration.MemoryAllocator.Allocate2D<Vector4>(
                this.windowHeight,
                destWidth,
                AllocationOptions.Clean);

            this.tempRowBuffer = configuration.MemoryAllocator.Allocate<Vector4>(this.sourceRectangle.Width);
            this.tempColumnBuffer = configuration.MemoryAllocator.Allocate<Vector4>(destWidth);

            this.currentWindow = new RowInterval(0, this.windowHeight);
        }

        public void Dispose()
        {
            this.transposedFirstPassBuffer.Dispose();
            this.tempRowBuffer.Dispose();
            this.tempColumnBuffer.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<Vector4> GetColumnSpan(int x, int startY)
        {
            return this.transposedFirstPassBuffer.GetRowSpan(x).Slice(startY - this.currentWindow.Min);
        }

        public void Initialize()
        {
            this.CalculateFirstPassValues(this.currentWindow);
        }

        public void FillDestinationPixels(RowInterval rowInterval, Buffer2D<TPixel> destination)
        {
            Span<Vector4> tempColSpan = this.tempColumnBuffer.GetSpan();

            for (int y = rowInterval.Min; y < rowInterval.Max; y++)
            {
                // Ensure offsets are normalized for cropping and padding.
                ResizeKernel kernel = this.verticalKernelMap.GetKernel(y - this.targetOrigin.Y);

                while (kernel.StartIndex + kernel.Length > this.currentWindow.Max)
                {
                    this.Slide();
                }

                ref Vector4 tempRowBase = ref MemoryMarshal.GetReference(tempColSpan);

                int top = kernel.StartIndex - this.currentWindow.Min;

                for (int x = 0; x < this.destWidth; x++)
                {
                    Span<Vector4> firstPassColumn = this.GetColumnSpan(x).Slice(top);

                    // Destination color components
                    Unsafe.Add(ref tempRowBase, x) = kernel.ConvolveCore(firstPassColumn);
                }

                Span<TPixel> targetRowSpan = destination.GetRowSpan(y);

                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, tempColSpan, targetRowSpan, this.conversionModifiers);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<Vector4> GetColumnSpan(int x)
        {
            return this.transposedFirstPassBuffer.GetRowSpan(x);
        }

        private void Slide()
        {
            int minY = this.currentWindow.Min + this.windowBandDiameter;
            int maxY = Math.Min(this.currentWindow.Max + this.windowBandDiameter, this.sourceRectangle.Height);
            this.currentWindow = new RowInterval(minY, maxY);
            this.CalculateFirstPassValues(this.currentWindow);
        }

        private void CalculateFirstPassValues(RowInterval window)
        {
            Span<Vector4> tempRowSpan = this.tempRowBuffer.GetSpan();
            for (int y = window.Min; y < window.Max; y++)
            {
                Span<TPixel> sourceRow = this.source.GetRowSpan(y);

                PixelOperations<TPixel>.Instance.ToVector4(
                    this.configuration,
                    sourceRow,
                    tempRowSpan,
                    this.conversionModifiers);

                // ref Vector4 firstPassBaseRef = ref this.buffer.Span[y - top];
                Span<Vector4> firstPassSpan = this.transposedFirstPassBuffer.Span.Slice(y - window.Min);

                for (int x = this.targetWorkingRect.Left; x < this.targetWorkingRect.Right; x++)
                {
                    ResizeKernel kernel = this.horizontalKernelMap.GetKernel(x - this.targetOrigin.X);
                    firstPassSpan[x * this.windowHeight] = kernel.Convolve(tempRowSpan);

                    // Unsafe.Add(ref firstPassBaseRef, x * this.sourceRectangle.Height) = kernel.Convolve(tempRowSpan);
                }
            }
        }
    }
}