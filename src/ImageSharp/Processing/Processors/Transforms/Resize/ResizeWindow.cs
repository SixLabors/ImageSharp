// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    internal class ResizeWindow<TPixel> : IDisposable
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

        private readonly ResizeKernelMap verticalKernelMap;

        private readonly Rectangle destWorkingRect;

        private readonly int diameter;

        private readonly int windowHeight;

        public ResizeWindow(
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
            this.destWorkingRect = destWorkingRect;
            this.startX = startX;

            this.diameter = verticalKernelMap.MaxDiameter;

            this.windowHeight = Math.Min(this.sourceRectangle.Height, 2 * this.diameter);

            this.buffer = configuration.MemoryAllocator.Allocate2D<Vector4>(
                this.windowHeight,
                destWidth,
                AllocationOptions.Clean);
            this.tempRowBuffer = configuration.MemoryAllocator.Allocate<Vector4>(this.sourceRectangle.Width);
        }

        public int Bottom { get; private set; }

        public int Top { get; private set; }

        public void Dispose()
        {
            this.buffer.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<Vector4> GetColumnSpan(int x, int startY)
        {
            return this.buffer.GetRowSpan(x).Slice(startY - this.Top);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<Vector4> GetColumnSpan(int x)
        {
            return this.buffer.GetRowSpan(x);
        }

        public void Initialize()
        {
            this.Initialize(0, this.windowHeight);
        }

        public void Slide()
        {
            int top = this.Top + this.diameter;
            int bottom = Math.Min(this.Bottom + this.diameter, this.sourceRectangle.Height);
            this.Initialize(top, bottom);
        }

        private void Initialize(int top, int bottom)
        {
            this.Top = top;
            this.Bottom = bottom;

            Span<Vector4> tempRowSpan = this.tempRowBuffer.GetSpan();
            for (int y = top; y < bottom; y++)
            {
                Span<TPixel> sourceRow = this.source.GetRowSpan(y);

                PixelOperations<TPixel>.Instance.ToVector4(
                    this.configuration,
                    sourceRow,
                    tempRowSpan,
                    this.conversionModifiers);

                //ref Vector4 firstPassBaseRef = ref this.buffer.Span[y - top];
                Span<Vector4> firstPassSpan = this.buffer.Span.Slice(y - top);

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