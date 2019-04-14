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
    class ResizeWindow<TPixel> : IDisposable
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

        private readonly Rectangle workingRectangle;

        public ResizeWindow(
            Configuration configuration,
            BufferArea<TPixel> source,
            PixelConversionModifiers conversionModifiers,
            ResizeKernelMap horizontalKernelMap,
            ResizeKernelMap verticalKernelMap,
            int destWidth,
            Rectangle workingRectangle,
            int startX)
        {
            this.configuration = configuration;
            this.source = source;
            this.sourceRectangle = source.Rectangle;
            this.conversionModifiers = conversionModifiers;
            this.horizontalKernelMap = horizontalKernelMap;
            this.verticalKernelMap = verticalKernelMap;
            this.workingRectangle = workingRectangle;
            this.startX = startX;
            this.buffer = configuration.MemoryAllocator.Allocate2D<Vector4>(
                this.sourceRectangle.Height,
                destWidth,
                AllocationOptions.Clean);
            this.tempRowBuffer = configuration.MemoryAllocator.Allocate<Vector4>(this.sourceRectangle.Width);

            this.Top = this.sourceRectangle.Top;

            this.Bottom = this.sourceRectangle.Bottom;
        }

        public int Bottom { get; private set; }

        public int Top { get; private set; }

        public void Dispose()
        {
            this.buffer.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<Vector4> GetColumnSpan(int x)
        {
            return this.buffer.GetRowSpan(x);
        }

        public void Initialize()
        {
            Span<Vector4> tempRowSpan = this.tempRowBuffer.GetSpan();
            for (int y = 0; y < this.sourceRectangle.Height; y++)
            {
                Span<TPixel> sourceRow = this.source.GetRowSpan(y);

                PixelOperations<TPixel>.Instance.ToVector4(
                    this.configuration,
                    sourceRow,
                    tempRowSpan,
                    this.conversionModifiers);

                ref Vector4 firstPassBaseRef = ref this.buffer.Span[y];

                for (int x = this.workingRectangle.Left; x < this.workingRectangle.Right; x++)
                {
                    ResizeKernel kernel = this.horizontalKernelMap.GetKernel(x - this.startX);
                    Unsafe.Add(ref firstPassBaseRef, x * this.sourceRectangle.Height) = kernel.Convolve(tempRowSpan);
                }
            }
        }
    }
}